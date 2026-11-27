using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Blood Totem (BloodTotem.cs), the Tank Hemomancer's 4th choice on the lobby's 4th key: the table and cards, the pick reaching
// the run and both screens, the real key path (6% health, a totem at the cursor, at most 420 px out), the veins gripping only
// within 180 px once it has risen (40% speed), 5 veins a totem, one totem a vein (no stacking), both screens agreeing on who
// is held, 3 totems at most (a 4th bursts the oldest), 14 s, mastery (6 a second drained) and Bloodroot (+20% reach, +3 s).
static partial class Program
{
    static void BloodTotemTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.BloodTotem);
        Check(def.Name == "Blood Totem" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Hemomancer && def.Role == Role.Tank
            && def.Cooldown == Game.BtCooldown && def.HealthCost == Game.BtCost, "Blood Totem: a Tank Hemomancer 1st ability on the 4th key, 6% health");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Hemomancer) == 4 && Upgrades.FirstAbility(Role.Tank, Kit.Hemomancer, 3) == AbilityId.BloodTotem
            && Enumerable.Range(0, 3).All(i => Upgrades.FirstAbility(Role.Tank, Kit.Hemomancer, i) != AbilityId.BloodTotem),
            "the Tank Hemomancer's 4th key is Blood Totem; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.BloodTotem) && Upgrades.All.Any(u => u.Name == "Bloodroot" && u.RequiresAbility == AbilityId.BloodTotem && u.Rarity == Rarity.Rare),
            "Blood Totem is a card for Tank Hemomancers who picked another, and Bloodroot a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Rooter", Role.Tank, 0, 3, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.BloodTotem && client.Game.Players[1].Slots[0].Id == AbilityId.BloodTotem, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f), aim = home + new V2(200f, 0f);
        var mine = new HashSet<int>();
        var hold = new Dictionary<int, V2>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; e.Pos = at; mine.Add(e.Id); hold[e.Id] = at; return e;
        };
        Action<float, bool> step = (secs, press) =>
        {
            for (int f = press ? -1 : 0; f < Math.Max(1, (int)Math.Round(secs / dt)); f++)  // (a press starts with a frame let go: the key is read on its way down)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; if (hold.ContainsKey(e.Id)) e.Pos = hold[e.Id]; }
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { Ability = press && f == 0, AimAt = aim });
                g.Update(dt); client.Game.Update(dt);
            }
        };
        Func<IEnumerable<Zone>> totems = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.BloodTotem);
        Action ready = () => { me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Hp = me.MaxHp; };
        Action reset = () =>
        {
            mine.Clear(); hold.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
            foreach (var z in totems().ToList()) g.Zones.Remove(z.Id);
            make(home + new V2(-380f, -330f));  // an enemy far from every totem keeps the wave going (a wave's end clears the zones)
            step(dt, false);
            ready(); me.Downed = false;
            me.MasteryXp.Remove(AbilityId.BloodTotem); me.Stats.BtReachMult = 1f; me.Stats.BtBonusTime = 0f;
        };
        Func<Enemy, bool> slowed = e => e.SlowT > 0f && Math.Abs(e.SlowMult - (1f - Game.BtSlow)) < 0.01f;

        // The real key path: 6% health, a totem at the cursor; the other screen sees it.
        reset();
        V2 spot = aim;
        var inside = make(spot + new V2(120f, 0f));
        var edge = make(spot + new V2(0f, 170f));
        var outside = make(spot + new V2(0f, -260f));
        float hp0 = me.Hp;
        step(dt, true);
        Check(totems().Count() == 1 && totems().First().Pos.DistanceTo(spot) < 1f, "pressing the ability key plants a totem at the cursor");
        Check(Math.Abs(hp0 - me.Hp - me.MaxHp * Game.BtCost) < 1f, "it costs 6% of max health (" + (hp0 - me.Hp).ToString("0.0") + ")");
        Check(Math.Abs(totems().First().Radius - Game.BtReach) < 0.1f, "its veins reach 180 px");
        Check(!slowed(inside), "nothing is gripped before it has risen");
        step(0.4f, false);
        Check(client.Game.Zones.Values.Count(z => z.Kind == ZoneKind.BloodTotem) == 1, "the other screen sees the totem");
        Check(slowed(inside) && slowed(edge), "enemies within reach are gripped: 40% speed");
        Check(!slowed(outside), "an enemy past the reach is not");
        var hostGrips = new List<Game.TotemGrip>(); var clientGrips = new List<Game.TotemGrip>();
        g.TotemGrips(hostGrips); client.Game.TotemGrips(clientGrips);
        Check(hostGrips.Count == 2 && hostGrips.Select(x => x.Enemy.Id).OrderBy(x => x).SequenceEqual(clientGrips.Select(x => x.Enemy.Id).OrderBy(x => x)),
            "both screens agree on who is held (" + hostGrips.Count + ", " + clientGrips.Count + ")");

        // A totem placed beyond 420 px comes down at 420 px, toward the cursor.
        reset();
        aim = home + new V2(900f, 0f);
        step(dt, true);
        Check(totems().Count() == 1 && Math.Abs(totems().First().Pos.DistanceTo(home) - Game.BtThrow) < 2f, "a far cursor plants it 420 px out");
        aim = home + new V2(200f, 0f);

        // Five veins a totem, nearest first.
        reset();
        var ring = Enumerable.Range(0, 8).Select(i => make(spot + V2.FromAngle(i * MathX.Tau / 8f) * (40f + i * 15f))).ToList();
        step(dt, true);
        step(0.4f, false);
        Check(ring.Count(slowed) == Game.BtVeins && ring.Take(5).All(slowed), "one totem holds its 5 nearest (" + ring.Count(slowed) + ")");

        // Totems don't stack, and a 4th bursts the oldest.
        reset();
        var both = make(spot + new V2(0f, 150f));
        step(dt, true);
        aim = spot + new V2(0f, 300f); ready(); step(dt, true);
        step(0.4f, false);
        g.TotemGrips(hostGrips);
        Check(totems().Count() == 2 && totems().All(z => z.Pos.DistanceTo(both.Pos) <= z.Radius) && hostGrips.Count(x => x.Enemy == both) == 1, "an enemy two totems reach is held by one vein");
        int first = totems().Min(z => z.Id);
        aim = spot + new V2(-200f, 0f); ready(); step(dt, true);
        Check(totems().Count() == 3, "three totems at once");
        aim = spot + new V2(-200f, 250f); ready(); step(dt, true);
        Check(totems().Count() == 3 && !g.Zones.ContainsKey(first), "a 4th bursts the oldest");
        Check(g.Effects.Any(f => f.Kind == FxKind.Boom), "the burst shows");
        aim = home + new V2(200f, 0f);

        // It stands 14 s.
        reset();
        step(dt, true);
        step(Game.BtLife - 0.5f, false);
        Check(totems().Count() == 1, "still standing at 13.5 s");
        step(1f, false);
        Check(!totems().Any(), "gone after 14 s");

        // Mastered: the veins drain 6 a second.
        reset();
        me.MasteryXp[AbilityId.BloodTotem] = 99999f;
        var drained = make(spot + new V2(80f, 0f));
        step(dt, true);
        step(Game.BtRise + 0.05f, false);
        drained.Hp = drained.MaxHp;
        step(2f, false);
        float lost = drained.MaxHp - drained.Hp;
        Check(lost > 10f * Might && lost < 18f * Might, "mastered: the veins drain about 6 a second, Blood Price on top (" + lost.ToString("0.0") + " in 2 s)");

        // Bloodroot: +20% reach and 3 s longer.
        reset();
        me.Stats.BtReachMult = 1.2f; me.Stats.BtBonusTime = 3f;
        step(dt, true);
        var tz = totems().First();
        Check(Math.Abs(tz.Radius - Game.BtReach * 1.2f) < 0.1f && Math.Abs(tz.Duration - (Game.BtLife + 3f)) < 0.01f, "Bloodroot: 20% more reach and 3 s longer");

    }
}
