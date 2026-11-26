using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Caltrop Run (CaltropRun.cs), the DPS Knight's 3rd choice (took Caught Steel's place): the table, the sprint, caltrops dropped
// only while moving and capped, the bounce and arming, the bite (damage, bleed, slow; heavies less, each enemy once), breaking
// after two, rusting away, the cooldown waiting, the other screen, Barbed Iron and mastery.
static partial class Program
{
    static void CaltropRunTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);

        var def = Upgrades.Ability(AbilityId.CaltropRun);
        Check(Upgrades.FirstAbility(Role.Dps, Kit.Knight, 2) == AbilityId.CaltropRun && def.Name == "Caltrop Run" && def.Cooldown == Game.CaltropCooldown,
            "Caltrop Run is the DPS Knight's 3rd choice, 12 s");
        Check(Upgrades.All.Any(u => u.Name == "Barbed Iron" && u.RequiresAbility == AbilityId.CaltropRun && u.Rarity == Rarity.Rare)
            && !Upgrades.All.Any(u => u.Name == "Forged Edge"), "its Rare card is Barbed Iron (Forged Edge is gone)");
        Check(OldNames.Ability("CaughtSteel") == "CaltropRun" && OldNames.Ability("BattleFury") == "CaltropRun", "old saves with Caught Steel load as Caltrop Run");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Dps", Role.Dps, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Dps2", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        Enemy anchor = null;  // one far-off enemy that keeps the wave going (between waves the caltrops are swept away)
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!keep.Contains(e) && e != anchor) e.Hp = 0f; };
        Func<EnemyType, V2, Enemy> Mob = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = 1e6f; e.FrozenT = 99f; e.Pos = at; keep.Add(e); return e;
        };
        V2 walk = V2.Zero;  // px a second the test walks the knight
        Action<float> Step = secs =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                Nap(1);
                me.Pos = me.Pos + walk * dt;
                host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(1000f, 450f) }); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(800, 450) }); client.Game.Update(dt);
                Clear();
            }
        };
        Func<List<Zone>> Trops = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.Caltrop).ToList();
        Action Reset = () =>
        {
            foreach (var e in keep) e.Hp = 0f;
            keep.Clear();
            Clear();
            walk = V2.Zero;
            g.Zones.Clear();
            me.CaltropT = 0f;
            me.Pos = new V2(300f, 450f);
            Step(0.1f);
            me.Slots[0].Id = AbilityId.CaltropRun;
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f;
        };
        Clear(); Step(0.2f);
        anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(1500f, 80f), 0f, 0, false });
        anchor.SpawnT = 0f; anchor.Hp = anchor.MaxHp = 1e9f; anchor.FrozenT = 1e6f; anchor.FireT = 1e6f;

        // --- Standing still drops nothing; the sprint ---
        Reset();
        float slow = me.Speed;
        cast.Invoke(g, new object[] { 1, 0, me.Pos, false });
        Step(0.5f);
        Console.WriteLine("  caltrop run: speed {0:0} -> {1:0}, {2} caltrops standing still, run left {3:0.0} s", slow, me.Speed, Trops().Count, me.CaltropT);
        Check(Trops().Count == 0, "standing still drops no caltrops");
        Check(Math.Abs(me.Speed / slow - Game.CaltropSpeed) < 0.01f, "the run is 20% faster");
        Check(client.Game.Players[1].CaltropT > 0f, "the other screen sees the run");
        float timer0 = me.Slots[0].Timer;
        Step(0.3f);
        Check(Math.Abs(me.Slots[0].Timer - timer0) < 0.01f, "the cooldown waits while you run");

        // --- A straight run: one every 34 px, capped at 16, landing behind ---
        walk = new V2(300f, 0f);
        Step(0.4f);  // 120 px
        int early = Trops().Count;
        Check(early >= 3 && early <= 4, "one caltrop per 34 px moved (" + early + " after 120 px)");
        var first = Trops().OrderBy(z => z.Id).First();
        Check(first.Pos.X < me.Pos.X - 60f && Math.Abs(first.Pos.Y - 450f) < Game.CaltropBounce + 1f, "they land behind you, near your path");
        Step(2.5f);  // 750 px more
        int all = Trops().Count;
        Console.WriteLine("  caltrop run: {0} after 120 px, {1} after the run, run left {2:0.00} s", early, all, me.CaltropT);
        Check(all == Game.CaltropMax, "the pouch holds 16");
        Check(client.Game.Zones.Values.Count(z => z.Kind == ZoneKind.Caltrop) == all, "every screen sees the caltrops");
        Step(0.5f);
        Check(me.CaltropT <= 0f && me.Slots[0].Timer > 0f, "the run ends once the pouch is empty, and the cooldown starts");

        // --- The bite: damage, bleed, slow; once per enemy; breaks after two ---
        Reset();
        cast.Invoke(g, new object[] { 1, 0, me.Pos, false });
        walk = new V2(300f, 0f);
        Step(0.3f);
        walk = V2.Zero;
        Step(0.3f);  // landed and armed
        var trop = Trops().OrderBy(z => z.Id).First();
        var grunt = Mob(EnemyType.Grunt, trop.Pos);
        Step(0.05f);
        float hit = 1e6f - grunt.Hp;
        Console.WriteLine("  caltrop run: the grunt took {0:0.0}, bleeding {1}, slowed to {2:0.00}, the caltrop has {3} catches left", hit, grunt.BleedT > 0f, grunt.SlowMult, trop.Leg);
        Check(hit >= Game.CaltropDamage * 0.95f && grunt.BleedT > 0f && Math.Abs(grunt.SlowMult - (1f - Game.CaltropSlow)) < 0.01f, "a caltrop cuts (12), bleeds and slows 40%");
        float hp1 = grunt.Hp;
        Step(0.3f);
        Check(trop.Leg == Game.CaltropUses - 1 && hp1 - grunt.Hp < Game.CaltropDamage * 0.5f, "standing on it doesn't cut again (only the bleed)");
        var heavy = Mob(EnemyType.Gatekeeper, trop.Pos);
        Step(0.05f);
        Console.WriteLine("  caltrop run: the heavy ({0}) slowed to {1:0.00}; the caltrop {2}", heavy.Def.Heavy, heavy.SlowMult, g.Zones.ContainsKey(trop.Id) ? "stands" : "broke");
        Check(!heavy.Def.Heavy || Math.Abs(heavy.SlowMult - (1f - Game.CaltropSlowHeavy)) < 0.01f, "heavy enemies are slowed only 20%");
        Check(!g.Zones.ContainsKey(trop.Id), "a caltrop breaks after its second enemy");

        // --- Rusting away ---
        var rest = Trops();
        Step(Game.CaltropLife + 0.3f);
        Check(rest.All(z => !g.Zones.ContainsKey(z.Id)), "unused caltrops are gone after 6 s");

        // --- Barbed Iron and mastery ---
        Reset();
        me.Stats.CaltropMore = 4; me.Stats.CaltropDamage = 4f;
        me.MasteryXp[AbilityId.CaltropRun] = 1e6f;
        cast.Invoke(g, new object[] { 1, 0, me.Pos, false });
        walk = new V2(320f, 0f);
        Step(2.6f);
        walk = V2.Zero;
        var cards = Trops();
        Console.WriteLine("  caltrop run: Barbed Iron and mastered: {0} caltrops, life {1:0} s, {2} catches each", cards.Count, cards.Count > 0 ? cards[0].Duration : 0f, cards.Count > 0 ? cards[0].Leg : 0);
        Check(cards.Count == Game.CaltropMax + 4, "Barbed Iron adds 4 caltrops");
        Check(cards.All(z => Math.Abs(z.Duration - Game.CaltropLifeMastered) < 0.01f && z.Leg == Game.CaltropUsesMastered), "mastered: they last 9 s and catch 3");
        Step(0.3f);
        var t2 = Trops().OrderBy(z => z.Id).First();
        var g2 = Mob(EnemyType.Grunt, t2.Pos);
        Step(0.05f);
        Check(1e6f - g2.Hp >= (Game.CaltropDamage + 4f) * 0.95f, "Barbed Iron's caltrops hit 4 harder");
        me.Stats.CaltropMore = 0; me.Stats.CaltropDamage = 0f;
        me.MasteryXp.Remove(AbilityId.CaltropRun);

    }
}
