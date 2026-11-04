using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Riot Gear (RiotGear.cs, Docs/RiotGear.md): blocks one hit, a half second of grace, a 30 s recharge; burns and gas go through.
static partial class Program
{
    static void RiotGearTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var damage = typeof(Game).GetMethod("Damage", flags);
        var give = typeof(Game).GetMethod("GivePerks", flags);

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        var prof = host.Game.ProfileOverride = new Profile();
        prof.BossBeaten(EnemyType.Boss, 5);
        prof.HuntEnded(EnemyType.Boss, true, 300f);
        Perks.Equip(prof, PerkId.RiotGear);
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Game.ProfileOverride = new Profile();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];

        Action calm = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; g.Bullets.Clear(); me.Hp = me.MaxHp; ally.Hp = ally.MaxHp; };
        Action<Player, float, string, bool> hit = (p, amount, src, dot) => (dot ? typeof(Game).GetMethod("DamageOverTime", flags) : damage).Invoke(g, dot ? new object[] { p, amount, src } : new object[] { p, amount, src });
        calm();

        Check(Game.HasPerk(me, PerkId.RiotGear) && !Game.HasPerk(ally, PerkId.RiotGear), "the host brings its equipped perk into the run; the friend, with none, has none");
        Check(Game.RiotReady(me) && me.RiotT == 0f, "the shield starts ready");

        // A hit is blocked: no health lost, the recharge starts, a half second of grace.
        float before = me.Hp, taken = me.Run.DamageTaken;
        hit(me, 20f, "Test shot", false);
        Check(me.Hp == before && me.Run.DamageTaken == taken && !me.Downed, "the first hit is blocked: no health lost and no damage counted");
        Check(Math.Abs(me.RiotT - Perks.RiotRecharge) < 0.001f && me.InvulnT >= Perks.RiotGraceT - 0.001f && !Game.RiotReady(me), "a block starts the 30 s recharge and half a second of invulnerability");

        // While it recharges hits land, and burning ground never touched it.
        hit(me, 20f, "Test shot", false);
        Check(me.Hp < before && me.RiotT > 0f, "a hit while it recharges lands");
        calm();
        me.RiotT = 0f;
        before = me.Hp;
        hit(me, 5f, "Burning Grounds", true);
        Check(me.Hp < before && Game.RiotReady(me), "a burn tick goes through and doesn't spend the shield");
        calm();

        // A teammate without the perk is hit normally.
        float allyHp = ally.Hp;
        hit(ally, 20f, "Test shot", false);
        Check(ally.Hp < allyHp, "a player without the perk takes the hit");
        calm();

        // The recharge: not back at 29 s, back at 30.
        hit(me, 20f, "Test shot", false);
        Check(me.RiotT > 29.9f, "blocked again, 30 s to go");
        Run(s, dt, 29f, null, calm);
        Check(me.RiotT > 0f && me.RiotT < 1.2f && !Game.RiotReady(me), "29 s later it is still down (" + me.RiotT.ToString("0.00") + " s left)");
        Run(s, dt, 1.5f, null, calm);
        Check(me.RiotT == 0f && Game.RiotReady(me), "after 30 s it is back");
        calm();
        before = me.Hp;
        hit(me, 20f, "Test shot", false);
        Check(me.Hp == before, "and it blocks again");

        // No perks in the Practice Range, or where runs aren't recorded (the Boss Lab, the lobby preview).
        var oldPlan = g.Practice;
        g.Practice = new PracticePlan();
        give.Invoke(g, new object[] { me, prof });
        Check(me.Perks.Length == 0 && me.RiotT == 0f, "the Practice Range takes the perks off");
        g.Practice = oldPlan;
        g.Unrecorded = true;
        give.Invoke(g, new object[] { me, prof });
        Check(me.Perks.Length == 0, "the Boss Lab takes the perks off");
        g.Unrecorded = false;
        give.Invoke(g, new object[] { me, prof });
        Check(Game.HasPerk(me, PerkId.RiotGear), "a real run gives them back");
        host.Leave(""); client.Leave("");
    }
}
