using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Wolf Whistle's pack (Core/WolfPack.cs): one wolf to start, one more every PackKillsPerWolf kills up to PackMax,
// a fresh wolf replacing the most wounded at full, PackShare of your damage soaked (most wounded wolf first, a wolf at 0
// gone), and Alt merging the small wolves into an alpha that is bigger, harder, faster and has their health; clients see
// the kill count, the alpha's size and a hurt wolf's health.
static partial class Program
{
    static void WolfPackTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var packKill = typeof(Game).GetMethod("PackKill", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var dash = typeof(Game).GetMethod("HostWeaponDash", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Summoner", Role.Dps, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        Action<float> play = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                me.Pos = new V2(500f, 450f); me.InvulnT = 1f; me.Hp = me.MaxHp;
                host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput()); client.Game.Update(dt);
            }
        };
        Func<List<Minion>> pack = () => g.Minions.Values.Where(m => m.Owner == 1 && m.Kind == MinionKind.Wolf && m.Companion).OrderBy(m => m.Id).ToList();
        Action<int> kills = n => { for (int i = 0; i < n; i++) packKill.Invoke(g, new object[] { me }); };
        play(0.5f);
        Check(me.Weapon.Id == WeaponId.WolfWhistle, "the DPS Summoner's first weapon is the Wolf Whistle");
        Check(pack().Count == 1 && pack()[0].MaxHp > 0f, "the pack starts with one wolf, with health");

        // Kills grow it, up to PackMax.
        kills(Game.PackKillsPerWolf - 1);
        int beforeTenth = pack().Count; byte counted = me.PackKills;
        play(0.2f);
        bool clientCount = client.Game.Players[1].PackKills == counted;
        kills(1);
        Check(beforeTenth == 1 && counted == Game.PackKillsPerWolf - 1 && pack().Count == 2 && me.PackKills == 0, "every " + Game.PackKillsPerWolf + " kills a wolf joins the pack");
        Check(clientCount, "clients see the kills towards the next wolf");
        kills(Game.PackKillsPerWolf * 6);
        Console.WriteLine("  wolf pack: {0} wolves after plenty of kills, next-wolf count {1}", pack().Count, me.PackKills);
        Check(pack().Count == Game.PackMax && me.PackKills == Game.PackKillsPerWolf, "the pack tops out at " + Game.PackMax + " (a full, healthy pack holds the next wolf ready)");

        // At full, a new wolf replaces the most wounded.
        var hurt = pack()[2]; hurt.Hp = hurt.MaxHp * 0.3f; int hurtId = hurt.Id;
        me.PackKills = 0;
        kills(Game.PackKillsPerWolf);
        Check(pack().Count == Game.PackMax && !g.Minions.ContainsKey(hurtId) && pack().All(m => m.Hp >= m.MaxHp - 0.01f), "with the pack full, a new wolf replaces the most wounded one");

        // Soaking: PackShare of a hit goes to the pack, most wounded wolf first; a wolf at 0 is gone.
        play(0.1f);
        float wolvesHp = pack().Sum(m => m.Hp), hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        float tookMe = hp0 - me.Hp, tookWolves = wolvesHp - pack().Sum(m => m.Hp);
        float share = tookWolves / Math.Max(0.01f, tookMe + tookWolves);
        Console.WriteLine("  wolf pack: a hit took {0:0.0} from you and {1:0.0} from the pack ({2:0.0%})", tookMe, tookWolves, share);
        Check(Math.Abs(share - Game.PackShare) < 0.005f, "the pack takes " + (Game.PackShare * 100f) + "% of the damage you take");
        me.Hp = me.MaxHp;
        var weak = pack()[1]; weak.Hp = 0.5f; int weakId = weak.Id;
        var next = pack().Where(m => m.Id != weakId).OrderBy(m => m.Hp).First();
        float nextHp = next.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        Check(!g.Minions.ContainsKey(weakId) && pack().Count == Game.PackMax - 1 && next.Hp < nextHp, "the most wounded wolf takes it first; at 0 it's gone, the rest spills to the next");
        play(0.2f);
        var hurt2 = pack()[0]; hurt2.Hp = hurt2.MaxHp * 0.5f;
        play(0.2f);
        Minion seen;
        Check(client.Game.Minions.TryGetValue(hurt2.Id, out seen) && Math.Abs(seen.GuardFrac - 0.5f) < 0.02f, "clients see a hurt wolf's health");
        hurt2.Hp = hurt2.MaxHp;

        // Alt: the small wolves merge into one alpha.
        kills(Game.PackKillsPerWolf);  // back to five
        float hpSum = pack().Sum(m => m.Hp), baseSpeed = Kits.Get(MinionKind.Wolf).Speed;
        float powerOne = pack()[0].Power * pack()[0].Scale;
        dash.Invoke(g, new object[] { 1, 31, me.Pos });
        var alpha = pack().Single();
        float bite = alpha.Power * alpha.Scale;
        Console.WriteLine("  wolf pack: alpha of {0}: scale {1:0.00}, bite x{2:0.00}, health {3:0}/{4:0} (wolves had {5:0})", alpha.Alpha, alpha.Scale, bite / powerOne, alpha.Hp, alpha.MaxHp, hpSum);
        Check(alpha.Alpha == 5 && Math.Abs(alpha.Scale - (1f + 4f * Game.AlphaScalePer)) < 0.001f, "Alt merges all five wolves into one bigger alpha");
        Check(Math.Abs(bite / powerOne - 5f * Game.AlphaBonus) < 0.01f && Math.Abs(alpha.Hp - hpSum) < 0.5f, "the alpha bites as hard as its wolves (and a bit more) and has their health");
        play(0.3f);
        Check(client.Game.Minions.TryGetValue(alpha.Id, out seen) && Game.AlphaSize(seen) == 5, "clients see the alpha's size");
        alpha.Pos = new V2(200f, 200f); me.Pos = new V2(800f, 450f);
        V2 start = alpha.Pos;
        host.Poll(); g.FixedStep(dt, new PlayerInput()); g.Update(dt);
        float ran = alpha.Pos.DistanceTo(start) / dt;
        Check(ran > baseSpeed * 1.25f, "the alpha runs faster for each wolf in it (" + ran.ToString("0") + " px/s vs " + baseSpeed + ")");

        // It takes one slot: kills bring small wolves beside it, and Alt again folds them in (up to AlphaMax).
        kills(Game.PackKillsPerWolf * 8);
        Check(pack().Count == Game.PackMax && pack().Count(m => m.Alpha > 0) == 1, "the alpha takes one slot in the pack");
        dash.Invoke(g, new object[] { 1, 31, me.Pos });
        alpha = pack().First(m => m.Alpha > 0);
        Check(alpha.Alpha == Game.AlphaMax && pack().Count == 1 + (4 - (Game.AlphaMax - 5)), "Alt again folds new wolves into the alpha, up to " + Game.AlphaMax);

        // A lone wolf doesn't merge with nothing.
        foreach (var m in pack()) g.Minions.Remove(m.Id);
        kills(Game.PackKillsPerWolf);
        dash.Invoke(g, new object[] { 1, 31, me.Pos });
        Check(pack().Count == 1 && pack()[0].Alpha == 0, "a single wolf can't merge on its own");
        s[0].Leave(""); s[1].Leave("");
    }
}
