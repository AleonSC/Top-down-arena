using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Claymore (Claymore.cs), the DPS Soldier's 3rd choice (Mark for Death's old place): the table, the pick reaching the run, the mine
// at the cursor (clamped to 220 px) facing away from you, the arming, the trigger cone (only what steps into it), the fuse and the
// wide blast (140 falling off with distance, nothing behind it), other screens seeing it, three per player, Shaped Charge, and
// mastery (it re-arms and goes off a second time).
static partial class Program
{
    static void ClaymoreTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.Claymore);
        Check(def.Name == "Claymore" && def.Choice == 2 && def.Kit == Kit.Soldier && def.Role == Role.Dps && def.Cooldown == 5f && def.ExtraCharges == 1,
            "Claymore: a 5 s DPS Soldier 1st ability with two charges, on the 3rd key");
        Check(Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 2) == AbilityId.Claymore && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 3) == AbilityId.DeployableTurret,
            "the DPS Soldier's 3rd key is Claymore; the turret stays on the 4th");
        Check(Upgrades.All.Any(u => u.Name == "Shaped Charge" && u.RequiresAbility == AbilityId.Claymore && u.Rarity == Rarity.Rare)
            && !Upgrades.All.Any(u => u.Name == "Exploit Weakness"), "Shaped Charge is its Rare card (Exploit Weakness is gone)");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Sapper", Role.Dps, 0, 2, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.Claymore && client.Game.Players[1].Slots[0].Id == AbilityId.Claymore, "the pick reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(400f, 450f);
        var pins = new Dictionary<int, V2>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; pins[e.Id] = at; e.Pos = at; return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !pins.ContainsKey(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear(); g.Minions.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; V2 p; if (pins.TryGetValue(e.Id, out p) && p.X > -1e5f) e.Pos = p; }
                me.Pos = mePos;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            pins.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Pos = mePos;
            me.MasteryXp.Remove(AbilityId.Claymore);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };
        Func<List<Zone>> mines = () => g.Zones.Values.Where(z => z.Kind == ZoneKind.Claymore).OrderBy(z => z.Id).ToList();
        Action<V2> cast = at => { me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, at, false }); };
        V2 spot = mePos + new V2(200f, 0f);
        Func<float, float> expect = d => 140f * (1f - 0.4f * Math.Min(1f, Math.Max(0f, d) / 210f));

        // Planted at the cursor facing away; arms, then the grunt in its cone trips it and the blast lands.
        reset();
        var front = make(EnemyType.Grunt, spot + new V2(100f, 0f));
        var flank = make(EnemyType.Grunt, spot + V2.FromAngle(40f * MathX.Pi / 180f) * 120f);  // in the blast, not the trigger
        var behind = make(EnemyType.Grunt, spot + new V2(-60f, 0f));
        var wide = make(EnemyType.Grunt, spot + V2.FromAngle(70f * MathX.Pi / 180f) * 100f);  // outside both cones
        float hf = front.Hp, hk = flank.Hp, hb = behind.Hp, hw = wide.Hp;
        cast(spot);
        var z0 = mines().FirstOrDefault();
        Check(z0 != null && z0.Pos.DistanceTo(spot) < 1f && Math.Abs(z0.Aim) < 0.05f && z0.GuardFrac < 0.1f,
            "the claymore goes down at the cursor, facing away from you, arming");
        step(0.4f);
        Check(front.Hp == hf && z0.GuardFrac < 1f, "nothing goes off while it arms");
        Check(client.Game.Zones.Values.Any(z => z.Kind == ZoneKind.Claymore && z.Pos.DistanceTo(spot) < 2f), "other screens see the claymore");
        step(0.3f);
        Check(z0.GuardFrac >= Game.ClaymoreTripped || front.Hp < hf, "armed, the grunt in its cone trips it");
        step(0.3f);
        float df = hf - front.Hp, dk = hk - flank.Hp;
        float wantF = expect(100f - front.Radius), wantK = expect(120f - flank.Radius);
        Console.WriteLine("  blast: front {0:0} (want {1:0}), flank {2:0} (want {3:0})", df, wantF, dk, wantK);
        Check(Math.Abs(df - wantF) < 6f, "the blast hits the grunt in front, less with distance");
        Check(Math.Abs(dk - wantK) < 6f, "and one to the side inside the wider blast cone");
        Check(behind.Hp == hb && wide.Hp == hw, "nothing behind it or outside the cone is hit");
        Check(mines().Count == 0 && client.Game.Effects.Any(fx => fx.Kind == FxKind.ClaymoreBlast), "it's spent, and the blast shows on other screens");

        // Something beside it doesn't trip it.
        reset();
        var side = make(EnemyType.Grunt, spot + new V2(0f, 90f));
        float hs = side.Hp;
        cast(spot);
        step(2f);
        Check(side.Hp == hs && mines().Count == 1 && mines()[0].GuardFrac == Game.ClaymoreArmed, "an enemy beside it, out of its cone, doesn't set it off");

        // The shove: a regular enemy is pushed away along the blast.
        var pushed = make(EnemyType.Grunt, spot + new V2(60f, 0f));
        pins[pushed.Id] = new V2(-1e6f, 0f);  // free to move
        step(0.05f);
        float x0 = pushed.Pos.X;
        step(0.6f);
        Check(pushed.Pos.X > x0 + 20f, "the blast shoves enemies away from it");

        // Out of reach, and three at most.
        reset();
        cast(mePos + new V2(900f, 0f));
        Check(mines().Count == 1 && Math.Abs(mines()[0].Pos.DistanceTo(mePos) - Game.ClaymoreRange) < 2f, "a far cursor: it goes down 220 px out");
        int first = mines()[0].Id;
        cast(mePos + new V2(0f, 150f)); cast(mePos + new V2(0f, -150f)); cast(mePos + new V2(-150f, 0f));
        step(dt * 2f);
        Check(mines().Count == 3 && mines().All(z => z.Id != first), "a 4th claymore removes the oldest");

        // Shaped Charge.
        reset();
        me.Stats.ClaymoreDmgMult = 1.4f;
        var strong = make(EnemyType.Grunt, spot + new V2(40f, 0f));
        float sl = strong.Hp;
        cast(spot);
        step(1f);
        Check(Math.Abs((sl - strong.Hp) - expect(40f - strong.Radius) * 1.4f) < 8f, "Shaped Charge: 40% more");
        me.Stats.ClaymoreDmgMult = 1f;

        // Mastery: it re-arms and goes off again.
        reset();
        me.MasteryXp[AbilityId.Claymore] = 1e6f;
        var target = make(EnemyType.Grunt, spot + new V2(80f, 0f));
        float t0 = target.Hp;
        cast(spot);
        Check(mines().Count == 1 && mines()[0].Marched > 0f, "mastered: it carries a spare blast");
        step(1f);
        float once = t0 - target.Hp;
        Check(once > 50f && mines().Count == 1 && mines()[0].GuardFrac < Game.ClaymoreArmed, "the first blast leaves it re-arming");
        step(1.5f);
        float twice = t0 - target.Hp;
        Check(twice > once * 1.8f && mines().Count == 0, "then it goes off a second time and is spent");
        me.MasteryXp.Remove(AbilityId.Claymore);
    }
}
