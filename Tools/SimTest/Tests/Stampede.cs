using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Stampede (Stampede.cs), the Tank Summoner's 4th choice on the lobby's 4th key: the table (only that class has a 4th with its
// first three unchanged), the pick reaching the run, a 7-beast line walking 320 px, the trample (20 once, bosses and heavies too),
// the shove (an enemy carried ahead of the herd; heavies stay; off-lane enemies untouched), the wall slam (+15 and a 0.5 s stun),
// other screens seeing the herd, the herd ending, mastery (9 beasts, charging back over what stands in its path) and Thundering Herd.
static partial class Program
{
    static void StampedeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.Stampede);
        Check(def.Name == "Stampede" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Summoner && def.Role == Role.Tank && def.Cooldown == 14f,
            "Stampede: a 14 s Tank Summoner 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Summoner) == 4 && Upgrades.FirstAbility(Role.Tank, Kit.Summoner, 3) == AbilityId.Stampede
            && Upgrades.FirstAbility(Role.Tank, Kit.Summoner, 0) == AbilityId.GolemWall && Upgrades.FirstAbility(Role.Tank, Kit.Summoner, 2) == AbilityId.ForcePact
            && Upgrades.FirstAbility(Role.Healer, Kit.Summoner, 3) != AbilityId.Stampede && Upgrades.FirstAbility(Role.Dps, Kit.Summoner, 3) != AbilityId.Stampede,
            "the Tank Summoner's 4th key is Stampede; its first three are unchanged, and the other Summoner roles don't get it");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.Stampede) && Upgrades.All.Any(u => u.Name == "Thundering Herd" && u.RequiresAbility == AbilityId.Stampede && u.Rarity == Rarity.Rare),
            "Stampede is a very rare card for Tank Summoners who picked another, and Thundering Herd a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Herd", Role.Tank, 0, 3, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.Stampede && client.Game.Players[1].Slots[0].Id == AbilityId.Stampede, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        Action<float, V2> step = (secs, aim) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.FrozenT = 99f; }  // (held still: only the herd moves them)
                me.Pos = mePos;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Action<V2> reset = at =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Slots[0].Charges = 1; me.Pos = mePos = at;
            me.MasteryXp.Remove(AbilityId.Stampede);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Func<Zone> herd = () => g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.Stampede);
        Action<V2> cast = aim => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, aim, false }); };
        V2 east = new V2(1400f, 450f);

        // The charge: a grunt in the lane is trampled once and carried; an off-lane one and a heavy are not.
        reset(new V2(500f, 450f));
        var inLane = make(EnemyType.Grunt, new V2(800f, 450f));
        var offLane = make(EnemyType.Grunt, new V2(800f, 700f));
        var heavy = make(EnemyType.Thunderhead, new V2(700f, 420f));
        float il = inLane.Hp, ol = offLane.Hp, hv = heavy.Hp;
        V2 heavyAt = heavy.Pos, offAt = offLane.Pos;
        cast(east);
        var z0 = herd();
        Check(z0 != null && Game.StampedeCount(z0) == 7 && Math.Abs(z0.Radius - 102f) < 0.5f && z0.Pos.DistanceTo(mePos + new V2(60f, 0f)) < 1f
            && Math.Abs(z0.T - Game.StampedeLife(false)) < 0.02f,
            "seven beasts in a 204 px line, a gate 60 px ahead, lasting the wind-up, the charge and the fade");
        step(Game.StampedeWind + 0.2f, east);
        Check(z0.Marched > 50f && client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Stampede && z.Marched > 20f && Math.Abs(z.Aim) < 0.05f),
            "the line walks out of the gate, and other screens see it");
        step(Game.StampedeRun(false) + 0.1f, east);
        Console.WriteLine("  stampede: in-lane grunt at {0:0}, took {1:0}; heavy took {2:0} and moved {3:0}", inLane.Pos.X, il - inLane.Hp, hv - heavy.Hp, heavy.Pos.DistanceTo(heavyAt));
        float lost = il - inLane.Hp;
        Check(lost > 19f && lost < 25f, "the grunt in its way is trampled for 20, once");
        Check(inLane.Pos.X > 860f, "and carried along ahead of the herd (it stood at x 800; the line ended near 900)");
        Check(hv - heavy.Hp > 19f && heavy.Pos.DistanceTo(heavyAt) < 40f, "a heavy is trampled for 20 but not shoved");
        Check(offLane.Hp == ol && offLane.Pos.DistanceTo(offAt) < 15f, "an enemy outside the line is left alone");
        step(Game.StampedeFade + 0.2f, east);
        Check(herd() == null && client.Game.Zones.Values.All(z => z.Kind != ZoneKind.Stampede), "the herd fades away, on every screen");

        // The wall: a grunt driven into the arena's edge takes the slam and is stunned.
        reset(new V2(g.ArenaMax.X - 330f, 450f));
        var wallie = make(EnemyType.Grunt, new V2(g.ArenaMax.X - 60f, 450f));
        float wl = wallie.Hp;
        bool stunned = false;
        cast(new V2(g.ArenaMax.X, 450f));
        for (int f = 0; f < (Game.StampedeLife(false)) / dt; f++)
        {
            step(dt, new V2(g.ArenaMax.X, 450f));
            stunned |= wallie.StunT > 0f;
        }
        float wallLost = wl - wallie.Hp;
        Console.WriteLine("  stampede: wall grunt took {0:0}, stunned {1}", wallLost, stunned);
        Check(wallLost > 33f && wallLost < 40f && stunned, "driven into the wall it takes 20 + 15 and is stunned");

        // Thundering Herd.
        reset(new V2(500f, 450f));
        me.Stats.StampedeDmgMult = 1.5f;
        var strong = make(EnemyType.Grunt, new V2(800f, 450f));
        float sl = strong.Hp;
        cast(east);
        step(Game.StampedeWind + Game.StampedeRun(false) + 0.05f, east);  // (before the Golem's slams)
        Check(sl - strong.Hp > 28f && sl - strong.Hp < 33f, "Thundering Herd: 50% harder (30 per trample)");
        me.Stats.StampedeDmgMult = 1f;

        // Mastery: nine beasts, and the herd turns and charges back over what stands in its path.
        reset(new V2(500f, 450f));
        me.MasteryXp[AbilityId.Stampede] = 1e6f;
        cast(east);
        var zc = herd();
        Check(zc != null && Game.StampedeCount(zc) == 9 && Math.Abs(zc.Radius - 136f) < 0.5f, "mastered: nine beasts in a 272 px line");
        step(Game.StampedeWind + Game.StampedeRun(true) + 0.1f, east);
        var late = make(EnemyType.Grunt, new V2(740f, 450f));  // walks into the path while the herd is turning
        float ll = late.Hp;
        step(Game.StampedeRun(true) + Game.StampedeTurn + 0.2f, east);
        Check(zc.Leg == 1 || herd() == null, "the herd turned and charged back");
        Check(ll - late.Hp > 19f && late.Pos.X < 740f, "and trampled and carried what stood in its path on the way back");
        me.MasteryXp.Remove(AbilityId.Stampede);
    }
}
