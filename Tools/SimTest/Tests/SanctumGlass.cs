using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Sanctum Glass (SanctumGlass.cs): the Healer Knight's 4th choice (pick 3 reaching the run), the pane turning enemy shots (red
// aimed ones too) into motes that heal the most hurt player, players' shots passing through, the cap and the shatter, Rose
// Window, the mastered pane, replication, running out and going down.
static partial class Program
{
    static void SanctumGlassTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        var def = Upgrades.Ability(AbilityId.SanctumGlass);
        Check(def.Name == "Sanctum Glass" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Knight && def.Role == Role.Healer && def.Cooldown == 16f,
            "Sanctum Glass: a 16 s Healer Knight 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Healer, Kit.Knight) == 4 && Upgrades.FirstAbility(Role.Healer, Kit.Knight, 3) == AbilityId.SanctumGlass
            && Upgrades.FirstAbility(Role.Healer, Kit.Knight, 2) == AbilityId.LayOnHands && Upgrades.AbilityLimit(Role.Dps, Kit.Knight) == 3,
            "the Healer Knight's 4th key is Sanctum Glass; its first three are unchanged, and the DPS Knight keeps three");
        Check(Upgrades.All.Any(u => u.Name == "Rose Window" && u.RequiresAbility == AbilityId.SanctumGlass), "Rose Window is its upgrade card");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Glass", Role.Healer, 0, 3, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var friend = g.Players[2];
        Check(me.FirstAbility == AbilityId.SanctumGlass && me.Slots[0].Id == AbilityId.SanctumGlass, "picking the 4th choice brings Sanctum Glass into the run as the 1st ability");

        V2 mePos = new V2(300f, 450f), friendPos = new V2(260f, 560f), aim = new V2(900f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                me.Pos = mePos; me.Aim = 0f; friend.Pos = friendPos; me.InvulnT = 1f; friend.InvulnT = 1f;
                host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.GlassT = 0f;
            me.Pos = mePos; me.Aim = 0f; me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Hp = me.MaxHp; friend.Hp = friend.MaxHp;
        };
        Action<int, byte> volley = (n, kind) =>
        {
            for (int i = 0; i < n; i++)
                shoot.Invoke(g, new object[] { kind, new V2(640f + i * 6f, 450f + (i % 9 - 4) * 15f), new V2(-260f, 0f), 0f, 6f, 5f, 0, false, 0f });
        };
        Action press = () => hostAbility.Invoke(g, new object[] { 1, 0, aim, false });

        // The cast: a pane 110 px ahead, facing your aim.
        reset();
        masteredField.SetValue(g, false);
        pin(new V2(1500f, 900f));  // (somewhere out of the way, so the wave doesn't end)
        press();
        Check(me.GlassT > Game.GlassTime - 0.01f && me.GlassPos.DistanceTo(mePos + new V2(Game.GlassFront, 0f)) < 1f && !me.GlassWide && Math.Abs(me.GlassCap - Game.GlassCap) < 0.01f,
            "the 1st ability raises the pane 110 px ahead for 6 s, with a 120 healing cap");

        // Shots turn into motes that heal the most hurt player.
        friend.Hp = friend.MaxHp * 0.4f; me.Hp = me.MaxHp * 0.9f;
        float before = friend.Hp;
        volley(6, Defs.SmallShot);
        step(1.4f);
        Check(!g.Bullets.Values.Any(b => b.Owner == 0), "every enemy shot that reaches the pane is gone");
        Check(g.Effects.Any(e => e.Kind == FxKind.GlassMote && (int)e.Pos2.X == friend.Id), "each one sends a mote to the most hurt player");
        Console.WriteLine("  sanctum glass: 6 small shots healed the friend {0:0.0} (sent {1:0.0})", friend.Hp - before, me.GlassHealed);
        Check(Math.Abs(me.GlassHealed - 6f * Game.GlassSmallHeal) < 0.01f && friend.Hp - before > 6f * Game.GlassSmallHeal - 0.5f, "a small shot heals 4");
        Check(client.Game.Players[1].GlassT > 0f && Math.Abs(client.Game.Players[1].GlassHealed - me.GlassHealed) < 0.01f, "other screens see the pane and how much it has healed");

        // Red aimed shots turn too, and big shots heal more.
        g.Effects.Clear();
        float sent = me.GlassHealed;
        volley(2, Defs.PierceShot);
        volley(1, Defs.BigShot);
        step(1.4f);
        Check(!g.Bullets.Values.Any(b => b.Owner == 0) && Math.Abs(me.GlassHealed - sent - 2f * Game.GlassSmallHeal - Game.GlassBigHeal) < 0.01f,
            "red aimed shots turn into healing too, and a big shot heals 10");

        // Players' own shots pass through.
        var target = pin(new V2(700f, 450f));
        int mine = g.Bullets.Count;
        shoot.Invoke(g, new object[] { Defs.HealerShot, mePos + new V2(30f, 0f), new V2(600f, 0f), 0f, 6f, 10f, me.Id, false, 0f });
        step(0.9f);
        Check(target.Hp < target.MaxHp, "your own shots pass through the glass");

        // Nobody hurt: the shot still turns to light, but nothing counts.
        me.Hp = me.MaxHp; friend.Hp = friend.MaxHp;
        sent = me.GlassHealed;
        volley(3, Defs.SmallShot);
        step(1.4f);
        Check(!g.Bullets.Values.Any(b => b.Owner == 0) && me.GlassHealed == sent, "with nobody hurt it still eats the shots, but they don't count toward the cap");

        // The cap: it shatters.
        friend.Hp = 10f;
        g.Effects.Clear();
        volley(40, Defs.SmallShot);
        step(2f);
        Check(me.GlassT == 0f && g.Effects.Any(e => e.Kind == FxKind.GlassShatter) && me.GlassHealed <= Game.GlassCap + 0.01f, "at 120 healing it shatters");
        Check(me.Slots[0].Charges == 0 || me.Slots[0].Timer > 0f, "the cooldown is running");

        // Running out: it shatters by itself.
        reset();
        pin(new V2(1500f, 900f));
        press();
        step(Game.GlassTime + 0.2f);
        Check(me.GlassT == 0f && g.Effects.Any(e => e.Kind == FxKind.GlassShatter), "after 6 s it shatters");

        // Rose Window: 40% more cap.
        reset();
        pin(new V2(1500f, 900f));
        me.Stats.GlassCapMult += 0.4f;
        press();
        Check(Math.Abs(me.GlassCap - Game.GlassCap * 1.4f) < 0.01f, "Rose Window: it can heal 40% more");
        me.Stats.GlassCapMult -= 0.4f;

        // Mastered: wider, motes heal 50% more.
        reset();
        pin(new V2(1500f, 900f));
        typeof(Game).GetMethod("CastSanctumGlass", flags).Invoke(g, new object[] { me, true });
        friend.Hp = friend.MaxHp * 0.4f;
        volley(1, Defs.SmallShot);
        step(1.4f);
        Check(me.GlassWide && Game.GlassHalf(me) == Game.GlassHalfWidthMastered && Math.Abs(me.GlassHealed - Game.GlassSmallHeal * Game.GlassMasteredHeal) < 0.01f,
            "mastered: wider, and each mote heals 50% more");

        // Going down: the pane is gone.
        me.Downed = true;
        step(dt);
        Check(me.GlassT == 0f, "going down takes the pane away");
        me.Downed = false;
        masteredField.SetValue(g, false);
        s[0].Leave(""); s[1].Leave("");
    }
}
