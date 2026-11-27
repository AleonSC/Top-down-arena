using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Deployable Turret (DeployableTurret.cs), the DPS Soldier's default ultimate (key 0 of the Ultimate row): the table, the pick reaching the
// run, the zone on both screens, the root (no walking), nothing fired before it unfolds or without Fire, the lane (first two
// enemies hurt, off-lane and a third untouched), the spool-up (a later second hits harder than the first), the overheat blast
// (inside 160 px only), quiet folding when you go down, mastery (14 s, pierce 4, double blast), Overclocked Barrels, and the loadout preview.
static partial class Program
{
    static void DeployTurretTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.DeployableTurret);
        Check(def.Name == "Deployable Turret" && def.Choice == AbilityDef.Ult && def.UltKey == 0 && def.Kit == Kit.Soldier && def.Role == Role.Dps && def.Cooldown == 60f,
            "Deployable Turret: the DPS Soldier's default ultimate, 60 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Soldier) == AbilityId.DeployableTurret && Upgrades.AbilityLimit(Role.Dps, Kit.Soldier) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 3) == AbilityId.TacticalVest
            && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0) == AbilityId.PoisonDart && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 2) == AbilityId.Claymore,
            "the turret took Artillery Assault's place; its first three unchanged and Tactical Vest the 4th choice");
        Check(!Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.DeployableTurret) && Upgrades.All.Any(u => u.Name == "Overclocked Barrels" && u.RequiresAbility == AbilityId.DeployableTurret && u.Rarity == Rarity.Rare)
            && OldNames.Ability("ArtilleryAssault") == "DeployableTurret",
            "no ability card for the turret (an ultimate), Overclocked Barrels a Rare card for it; old saves' Artillery Assault loads as the turret");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Gunner", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.DeployableTurret && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.DeployableTurret, "the ultimate reaches the run, on both screens");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, Enemy> make = (type, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        bool pin = true;
        Action<float, bool> step = (secs, fire) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.FrozenT = 99f; }
                if (pin) me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(300f, 0f), Fire = fire });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Func<Zone> turret = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.DeployTurret);
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Pos = home; pin = true; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.DeployableTurret);
            make(EnemyType.Grunt, new V2(1500f, 120f));  // far off: the wave goes on
        };

        // The real key path (no direct HostAbility call): a press of the ultimate key from cold, then Fire.
        reset();
        make(EnemyType.Grunt, home + new V2(150f, 0f));
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ultimate = true, AimAt = home + new V2(300f, 0f) });
        g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(300f, 0f) });
        Check(turret() != null, "pressing the ability key casts it (the real input path)");
        step(1.5f, true);
        var keyed = g.Enemies.Values.First(e => mine.Contains(e.Id) && e.Pos.X < home.X + 200f);
        Check(lost(keyed) > 0f, "and holding Fire then shoots (the real input path)");

        // The cast: a zone on both screens lasting drop + 20 s + overheat, a 30% seat, you rooted.
        reset();
        var e1 = make(EnemyType.Grunt, home + new V2(150f, 0f));
        var e2 = make(EnemyType.Grunt, home + new V2(250f, 0f));
        var e3 = make(EnemyType.Grunt, home + new V2(350f, 0f));
        var off = make(EnemyType.Grunt, home + new V2(250f, 120f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        var z = turret();
        Check(z != null && Math.Abs(z.Duration - (Game.DtDeploy + Game.DtLife + Game.DtDying)) < 0.01f && z.BlockHp == Game.DtHp && z.GuardFrac == 1f && z.Pos.DistanceTo(home) < 1f, "the cast drops a turret on your spot for 1.1 + 20 + 0.8 s with 350 health");
        step(0.1f, false);
        Check(client.Game.Zones.Values.Any(q => q.Kind == ZoneKind.DeployTurret && q.Owner == 1), "the other screen sees it");
        pin = false;
        Vector2Move(g, me, dt, hostS, client);
        pin = true;
        step(0.3f, true);
        Check(lost(e1) == 0f && lost(e2) == 0f, "nothing fires while it is still unfolding, even holding Fire");

        // Fire: the first second spools up, the lane takes the first two, the rest nothing.
        step(0.8f, true);
        float first = lost(e1);
        Check(first > 0f, "holding Fire once it is up shoots down the lane");
        Check(lost(e2) > 0f && lost(e3) == 0f && lost(off) == 0f, "the round pierces 2: the near two are hurt, the third and an off-lane enemy are not");
        float before = lost(e1);
        step(1f, true);
        float second = lost(e1) - before;
        step(1f, true);
        float third = lost(e1) - before - second;
        Check(second > first * 1.05f && third >= second * 0.9f, "the rate spools up: a later second hits harder than the first (" + first.ToString("0") + " then " + second.ToString("0") + " then " + third.ToString("0") + ")");
        Check(Math.Abs(g.Zones.Values.First(q => q.Kind == ZoneKind.DeployTurret).Aim) < 0.2f, "it turned onto your aim");
        float idle = lost(e1);
        step(1f, false);
        Check(lost(e1) == idle, "no Fire, no rounds");

        // The end: it stops for the overheat, then blows for 80 within 160 px only.
        mine.Remove(off.Id); g.Enemies.Remove(off.Id);
        var near = make(EnemyType.Grunt, home + new V2(0f, 100f)); var far = make(EnemyType.Grunt, home + new V2(0f, 260f));
        step(Math.Max(0f, z.T - 0.7f), true);
        float pre = lost(e1);
        step(0.5f, true);
        Check(lost(e1) == pre, "it holds fire while it overheats");
        step(0.6f, false);
        Check(turret() == null, "it is gone when its time is up");
        Check(lost(near) >= Game.DtBlastDamage * 0.95f && lost(far) == 0f, "the blast hurts the enemy within 160 px (80) and not the one 260 px out");

        // Going down folds it away quietly.
        reset();
        var q1 = make(EnemyType.Grunt, home + new V2(100f, 100f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        step(0.2f, false);
        me.Downed = true;
        step(0.2f, false);
        Check(turret() == null && lost(q1) == 0f, "going down folds it away with no blast");

        // Its health: hits meant for you wear the turret first; what is left goes on to you; at 0 it is wrecked and throws you clear.
        reset();
        var damage = typeof(Game).GetMethod("Damage", flags);
        me.DevGod = false;
        g.DevSetGod(2, true);
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        step(0.2f, false);
        float hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 100f, "Test" });
        var zh = turret();
        Check(me.Hp == hp0 && zh.BlockHp < Game.DtHp - 1f && zh.BlockHp > 0f, "a hit meant for you wears the turret instead of your health");
        step(0.3f, false);
        Check(client.Game.Zones.Values.Any(q => q.Kind == ZoneKind.DeployTurret && q.GuardFrac < 1f && Math.Abs(q.GuardFrac - zh.GuardFrac) < 0.01f), "the other screen sees its health fall");
        damage.Invoke(g, new object[] { me, 5000f, "Test" });
        Check(turret() != null && turret().Radius == 0f && !g.Seated(me) && me.Hp < hp0, "wrecked: you are thrown clear (the rest of the hit reaches you) and it starts to overheat");
        step(1f, false);
        Check(turret() == null, "and it blows");
        me.DevGod = true;

        // Pressing the key again steps you out early: the guns stop, you are free, no cooldown is spent, and it still blows after 0.8 s.
        reset();
        var d1 = make(EnemyType.Grunt, home + new V2(0f, 90f));
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        step(1.6f, true);
        float dealt = lost(d1);
        int chargesBefore = me.Slots[Upgrades.UltSlot].Charges;
        float cdBefore = me.Slots[Upgrades.UltSlot].Timer;
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ultimate = true, AimAt = home + new V2(300f, 0f) });
        var zo = turret();
        Check(zo != null && zo.Radius == 0f && zo.T <= Game.DtDying + 0.05f && !g.Seated(me), "pressing the key again lets you out and starts the overheat now");
        Check(me.Slots[Upgrades.UltSlot].Charges == chargesBefore && me.Slots[Upgrades.UltSlot].Timer >= cdBefore - 0.5f, "stepping out spends no extra cooldown");
        pin = false;
        Vector2Move(g, me, dt, hostS, client, true);
        pin = true;
        step(0.3f, true);
        Check(turret() != null && lost(d1) == dealt, "once you are out it fires no more");
        step(0.8f, false);
        Check(turret() == null && lost(d1) >= dealt + Game.DtBlastDamage * 0.95f, "it still blows after its fuse");

        // Mastery and the card.
        reset();
        me.MasteryXp[AbilityId.DeployableTurret] = 99999f;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, home + new V2(300f, 0f), false });
        var zm = turret();
        Check(zm != null && zm.Cap && Math.Abs(zm.Duration - (Game.DtDeploy + Game.DtLifeMastered + Game.DtDying)) < 0.01f, "mastered: it fires 26 s");
        var m1 = make(EnemyType.Grunt, home + new V2(150f, 0f)); var m2 = make(EnemyType.Grunt, home + new V2(200f, 0f)); var m3 = make(EnemyType.Grunt, home + new V2(250f, 0f));
        var m4 = make(EnemyType.Grunt, home + new V2(300f, 0f)); var m5 = make(EnemyType.Grunt, home + new V2(350f, 0f));
        step(2.5f, true);
        Check(lost(m4) > 0f && lost(m5) == 0f, "mastered: rounds pierce 4");

        // The lobby's loadout preview: it drops the turret, holds the trigger so it fires on the dummies, then steps out (it blows).
        var show = new Showcase();
        show.Show(Role.Dps, Kit.Soldier, 0, AbilityId.DeployableTurret, true, true, 0);
        bool up = false, hurt = false, out_ = false;
        for (int i = 0; i < 60 * 10; i++)
        {
            show.Step(dt);
            var sz = show.Game.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.DeployTurret);
            up |= sz != null && show.Game.Seated(show.Me);
            hurt |= up && show.Game.Enemies.Values.Any(e => e.Hp < e.MaxHp);
            out_ |= up && sz != null && sz.Radius <= 0f;
        }
        Check(up && hurt && out_, "the loadout preview drops it, fires on the dummies and steps out");
        show.Stop();
    }

    static void Vector2Move(Game g, Player me, float dt, Session hostS, Session client, bool free = false)
    {
        V2 at = me.Pos;
        for (int f = 0; f < 0.4f / dt; f++)
        {
            hostS.Poll(); client.Poll();
            g.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), AimAt = at + new V2(300f, 0f) });
        }
        if (free)
            Check(me.Pos.DistanceTo(at) > 20f, "out of the seat you can walk again");
        else
            Check(me.Pos.DistanceTo(at) < 1f, "sitting in it you are rooted: holding a direction goes nowhere");
    }
}
