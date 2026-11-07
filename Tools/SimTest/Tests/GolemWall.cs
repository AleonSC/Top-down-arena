using System.Linq;
using System;
using System.Reflection;
using Arena;

// Golem Wall (Minions.cs): three big basalt golems that have no timer, wear from every enemy shot, break at zero health, and are replaced by a recast.
static partial class Program
{
    static void GolemWallTest(float dt)
    {
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var block = typeof(Game).GetMethod("BlockedBySummons", flags);
        var mastered = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Summoner);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        me.Slots[0].Id = AbilityId.GolemWall;
        Check(Kits.Get(MinionKind.Wall).Radius == 38f, "a golem is 38 px across the radius (was 22)");
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        g.Enemies.Clear(); g.Bullets.Clear();
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(1500f, 100f), 0f, 0, false });  // keeps the wave from ending
        anchor.Hp = anchor.MaxHp = 1e5f; anchor.SpawnT = 0f;
        Action calm = () => { anchor.Pos = new V2(1500f, 100f); g.Bullets.Clear(); me.Pos = new V2(300f, 450f); };
        me.Pos = new V2(300f, 450f); me.Aim = 0f;
        Func<System.Collections.Generic.List<Minion>> walls = () => g.Minions.Values.Where(m => m.Kind == MinionKind.Wall).ToList();
        mastered.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        var w = walls();
        Check(w.Count == 3, "Golem Wall raises three golems (" + w.Count + ")");
        Check(w.All(m => m.T < 0f), "they have no timer");
        var ys = w.Select(m => m.Pos.Y).OrderBy(y => y).ToList();
        Check(Math.Abs(ys[1] - ys[0] - Game.GolemWallGap) < 0.5f && Math.Abs(ys[2] - ys[1] - Game.GolemWallGap) < 0.5f, "they stand " + Game.GolemWallGap + " px apart, touching");
        Check(w.All(m => m.Pos.X - me.Pos.X > 100f), "and ahead of the caster");

        // Standing in fire for 25 s (far past the old 6 s) with nothing shooting at them.
        Run(s, dt, 25f, null, calm);
        Check(walls().Count == 3 && walls().All(m => m.BlockHp == m.Def.BlockCapacity), "after 25 s untouched, all three still stand at full health");

        // Every enemy shot wears one down (not just a boss's), until it breaks.
        var target = walls()[1];
        float before = target.BlockHp;
        Func<Minion, bool> shoot = m => (bool)block.Invoke(g, new object[] { new Bullet { Pos = m.Pos + new V2(48f, 0f), Vel = new V2(-900f, 0f), Radius = 4f, Dmg = 10f } });
        Check(shoot(target), "a shot at a golem is blocked");
        Check(target.BlockHp < before, "...and wears it down (" + before + " -> " + target.BlockHp + ")");
        int shots = 1;
        for (; shots < 400 && target.T < 0f; shots++) shoot(target);
        Check(target.T > 0f && target.T < 0.1f, "at zero health it breaks (after " + shots + " shots)");
        Run(s, dt, 0.5f, null, calm);
        Check(walls().Count == 2 && !g.Minions.ContainsKey(target.Id), "the broken golem is gone and the other two stand");

        // A recast replaces the old wall, and the capstone makes five.
        var oldIds = walls().Select(m => m.Id).ToList();
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        Check(walls().Count == 3 && walls().All(m => !oldIds.Contains(m.Id)), "casting again replaces the old golems with three new ones");
        mastered.SetValue(g, true);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        mastered.SetValue(g, false);
        Check(walls().Count == 5, "mastered, it raises five (" + walls().Count + ")");
        s[0].Leave(""); s[1].Leave("");
    }
}
