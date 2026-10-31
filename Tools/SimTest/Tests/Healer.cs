using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Healers alone: the aura heals you (1 HP/s, +1 per Sanctuary), a Healer's overhealing becomes a short shield,
// Martyr wards you when nobody's down, a Phoenix brings you back, and Haven heals faster with nobody to revive.
static partial class Program
{
    static void HealerSoloTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var heal = typeof(Game).GetMethod("Heal", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);

        // A solo Healer of this class with this Space ability, in a fight with one frozen dummy far away.
        Func<Kit, int, Tuple<Session, Action<float>>> Solo = (kit, choice) =>
        {
            var host = new Session { AutoStart = 1 };
            host.Host(nextPort++, "Solo", Role.Healer, 0, choice, kit);
            var g = host.Game;
            g.Bot = false;
            host.StartMatch();
            for (int f = 0; f < 360 && g.State != MatchState.Fighting; f++) Step(host, dt);
            var dummy = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(1400f, 800f), 0f, 0, false });
            dummy.Hp = dummy.MaxHp = 1e6f; dummy.SpawnT = 0f;
            Action<float> run = secs =>
            {
                for (int f = 0; f < secs * 60; f++)
                {
                    ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
                    foreach (var e in g.Enemies.Values) if (e != dummy) e.Hp = 0f;
                    dummy.Pos = new V2(1400f, 800f); dummy.FrozenT = 1f; dummy.FireT = 99f;
                    g.Bullets.Clear();
                    g.Players[1].Pos = new V2(300f, 450f);
                    host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(400f, 450f) }); g.Update(dt);
                }
            };
            run(0.2f);
            return Tuple.Create(host, run);
        };
        Func<Game, float, float> Gain = null;

        // The aura heals you 1 HP/s (on top of everyone's 0.5), +1 per Sanctuary.
        var a = Solo(Kit.Soldier, 1);  // with Haven
        var ag = a.Item1.Game; var am = ag.Players[1];
        Gain = (g, secs) => { var p = g.Players[1]; p.Hp = p.MaxHp * 0.5f; float h = p.Hp; a.Item2(secs); return p.Hp - h; };
        float plain = Gain(ag, 2f);
        ag.GrantUpgrade(1, U("Sanctuary"));
        float one = Gain(ag, 2f);
        ag.GrantUpgrade(1, U("Sanctuary"));
        float two = Gain(ag, 2f);
        Console.WriteLine("  healer alone: healed {0:0.0} / {1:0.0} / {2:0.0} in 2 s with 0 / 1 / 2 Sanctuary", plain, one, two);
        Check(Math.Abs(plain - 3f) < 0.3f && Math.Abs(one - 5f) < 0.3f && Math.Abs(two - 7f) < 0.3f,
            "the aura heals the Healer 1 HP/s, +1 per Sanctuary (plus everyone's 0.5 regeneration)");

        // Overhealing from a Healer becomes a shield (up to 20% of max health); other healing doesn't.
        am.Hp = am.MaxHp; am.ShieldHp = 0f;
        heal.Invoke(ag, new object[] { am, 15f, am, null });
        float shield30 = am.ShieldHp, shieldT = am.ShieldT;
        heal.Invoke(ag, new object[] { am, 1000f, am, null });
        float capped = am.ShieldHp;
        am.ShieldHp = 0f;
        heal.Invoke(ag, new object[] { am, 15f, null, "test" });
        Console.WriteLine("  overheal: 15 past full -> shield {0:0} for {1:0} s; 1000 -> {2:0} (cap {3:0}); from no Healer -> {4:0}", shield30, shieldT, capped, am.MaxHp * 0.2f, am.ShieldHp);
        Check(Math.Abs(shield30 - 15f) < 0.01f && shieldT >= 3.9f && Math.Abs(capped - am.MaxHp * 0.2f) < 0.01f && am.ShieldHp == 0f,
            "a Healer's healing past full health becomes a short shield, up to 20% of max health");

        // Haven alone: no one to revive, so it heals 50% faster.
        Check(am.Slots[0].Id == AbilityId.Haven, "the Soldier Healer's second Space ability is Haven");
        am.Hp = am.MaxHp * 0.3f; am.ShieldHp = 0f;
        cast.Invoke(ag, new object[] { 1, 0, am.Pos, false });
        float hv = am.Hp;
        a.Item2(1f);
        float havenGain = am.Hp - hv;
        Console.WriteLine("  haven alone: {0:0.0} healed in 1 s (zone 15 x 1.5 + aura 3 + 0.5)", havenGain);
        Check(havenGain > 15f * 1.5f + 3f, "alone, Haven heals 50% faster (nobody to revive)");
        a.Item1.Leave("");

        // Martyr alone: pay 40%, and you're warded: going down puts you straight back up at 50%.
        var m = Solo(Kit.BloodMage, 2);
        var mg = m.Item1.Game; var mm = mg.Players[1];
        Check(mm.Slots[0].Id == AbilityId.Martyr, "the Hemomancer Healer's third Space ability is Martyr");
        float before = mm.Hp;
        cast.Invoke(mg, new object[] { 1, 0, mm.Pos, false });
        Console.WriteLine("  martyr alone: paid {0:0}, ward {1:0.0} s", before - mm.Hp, mm.WardT);
        Check(mm.WardT > 11f && before - mm.Hp > mm.MaxHp * 0.35f, "alone, Martyr wards you for 12 s (instead of doing nothing)");
        m.Item2(1f);
        mm.InvulnT = 0f; mm.ShieldHp = 0f;
        damage.Invoke(mg, new object[] { mm, mm.Hp + 500f, "test" });
        Console.WriteLine("  martyr ward: after a lethal hit, downed {0}, health {1:0} of {2:0}", mm.Downed, mm.Hp, mm.MaxHp);
        Check(!mm.Downed && Math.Abs(mm.Hp - mm.MaxHp * 0.5f) < 1f && mm.WardT <= 0f, "the ward puts you straight back up at 50%, once");
        m.Item2(2f);
        mm.InvulnT = 0f; mm.ShieldHp = 0f;
        damage.Invoke(mg, new object[] { mm, mm.Hp + 500f, "test" });
        m.Item2(0.2f);
        Check(mm.Downed && mg.State == MatchState.GameOver, "without the ward, going down alone ends the run");
        m.Item1.Leave("");

        // Phoenix alone: going down while it's out, it brings you back.
        var f2 = Solo(Kit.Summoner, 1);
        var fg = f2.Item1.Game; var fm = fg.Players[1];
        Check(fm.Slots[0].Id == AbilityId.Phoenix, "the Summoner Healer's second Space ability is Phoenix");
        cast.Invoke(fg, new object[] { 1, 0, fm.Pos, false });
        f2.Item2(0.5f);
        fm.InvulnT = 0f; fm.ShieldHp = 0f;
        damage.Invoke(fg, new object[] { fm, fm.Hp + 500f, "test" });  // like a bullet: after the minions' turn
        f2.Item2(0.3f);
        Console.WriteLine("  phoenix alone: downed {0}, state {1}, health {2:0}", fm.Downed, fg.State, fm.Hp);
        Check(!fm.Downed && fg.State != MatchState.GameOver && fm.Hp > 0f, "alone, a Phoenix brings you back when you go down");
        f2.Item1.Leave("");
    }
}
