using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Cannon Call (CannonCall.cs), the DPS Knight's 4th choice on the lobby's 4th key: the table and cards, the pick reaching the
// run, the real key path, the mark clamped to its reach, one effect a ball on both screens, nothing landing before the ball
// does, the core and edge damage, the shove out and stun, nothing outside the ring, ordinary shots knocked down, the half
// cooldown back for 3 kills (and not for 2), mastery (two more balls) and Siege Powder.
static partial class Program
{
    static void CannonCallTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.CannonCall);
        Check(def.Name == "Cannon Call" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Knight && def.Role == Role.Dps && def.Cooldown == 12f,
            "Cannon Call: a 12 s DPS Knight 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Knight) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Knight, 3) == AbilityId.CannonCall
            && Upgrades.FirstAbility(Role.Dps, Kit.Knight, 0) == AbilityId.BladedDive && Upgrades.FirstAbility(Role.Dps, Kit.Knight, 2) == AbilityId.CaltropRun,
            "the DPS Knight's 4th key is Cannon Call; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.CannonCall) && Upgrades.All.Any(u => u.Name == "Siege Powder" && u.RequiresAbility == AbilityId.CannonCall && u.Rarity == Rarity.Rare),
            "Cannon Call is a very rare card for DPS Knights who picked another, and Siege Powder a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Gunner", Role.Dps, 0, 3, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.CannonCall && client.Game.Players[1].Slots[0].Id == AbilityId.CannonCall, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f), mark = home + new V2(300f, 0f);
        var mine = new HashSet<int>();
        Func<V2, float, Enemy> make = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; mine.Add(e.Id); return e;
        };
        var pinned = new Dictionary<int, V2>();
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values)
                {
                    e.FireT = 99f;
                    V2 p;
                    if (pinned.TryGetValue(e.Id, out p)) e.Pos = p;  // stood still until the slam
                }
                me.Pos = home;
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mark });
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Action reset = () =>
        {
            mine.Clear(); pinned.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Pos = home; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.CannonCall); me.Stats.CannonDamage = 0f; me.Stats.CannonWide = 0f;
            make(new V2(1500f, 120f), 1e6f);  // far off: the wave goes on
        };

        // The real key path, the mark, and one effect on both screens.
        reset();
        var core = make(mark, 1e6f);
        var edge = make(mark + new V2(0f, 105f), 1e6f);
        var outside = make(mark + new V2(0f, -190f), 1e6f);
        foreach (var e in new[] { core, edge, outside }) pinned[e.Id] = e.Pos;
        step(dt);
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = mark });
        Check(me.Slots[0].Timer > 11f, "pressing the ability key casts it: the 12 s cooldown runs");
        var fx = g.Effects.Where(f => f.Kind == FxKind.CannonCall).ToList();
        Check(fx.Count == 1 && fx[0].Pos2.DistanceTo(mark) < 1f, "one ball, marked at the cursor");
        step(0.2f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.CannonCall) == 1, "the other screen sees the ball coming");
        step(Game.CcLandT - 0.35f);
        Check(lost(core) == 0f && lost(edge) == 0f, "nothing lands before the ball does");
        pinned.Clear();
        step(0.2f);
        Console.WriteLine("  cannon call: core {0:0}, edge {1:0}, outside {2:0}; edge shoved {3:0} px", lost(core), lost(edge), lost(outside), edge.Pos.DistanceTo(mark) - 105f);
        Check(lost(core) >= Game.CcDamage && lost(core) < Game.CcDamage * 1.6f, "the core takes 70");
        Check(Math.Abs(lost(edge) / lost(core) - Game.CcEdgeDamage / Game.CcDamage) < 0.05f, "the edge takes half (35)");
        Check(lost(outside) == 0f, "nothing outside the blast ring is hit");
        step(0.3f);
        Check(edge.Pos.DistanceTo(mark) > 105f + Game.CcShoveMin * 0.8f && edge.FrozenT > 0f, "the slam shoves the edge enemy outward and stuns it");

        // Ordinary shots in the ring are knocked down; red aimed shots fly on.
        reset();
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        hostAbility.Invoke(g, new object[] { 1, 0, mark, false });
        for (int f = 0; f < (Game.CcLandT - 0.02f) / dt; f++) { me.Pos = home; g.FixedStep(dt, new PlayerInput { AimAt = mark }); }
        var small = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, mark + new V2(40f, 0f), new V2(0f, 1f), 0f, 3f, 5f, 0, false, 0f });
        var red = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.PierceShot, mark + new V2(-40f, 0f), new V2(0f, 1f), 0f, 3f, 5f, 0, false, 0f });
        for (int f = 0; f < 0.1f / dt; f++) { me.Pos = home; g.FixedStep(dt, new PlayerInput { AimAt = mark }); }
        Check(!g.Bullets.ContainsKey(small.Id) && g.Bullets.ContainsKey(red.Id), "the slam knocks ordinary shots in the ring out of the air; red aimed shots fly on");

        // The reach: a far cursor marks no farther than 420 px.
        reset();
        hostAbility.Invoke(g, new object[] { 1, 0, home + new V2(900f, 0f), false });
        var far = g.Effects.First(f => f.Kind == FxKind.CannonCall);
        Check(Math.Abs(far.Pos2.DistanceTo(home) - Game.CcReach) < 2f, "the mark lands no farther than 420 px");

        // Three kills: half the cooldown back. Two: nothing back.
        foreach (int kills in new[] { 3, 2 })
        {
            reset();
            for (int i = 0; i < kills; i++) make(mark + new V2(i * 20f - 20f, 10f), 10f);
            hostAbility.Invoke(g, new object[] { 1, 0, mark, false });
            me.Slots[0].Timer = def.Cooldown;
            step(Game.CcLandT + 0.2f);
            float left = me.Slots[0].Timer;
            if (kills == 3)
                Check(left < def.Cooldown * 0.5f && left > def.Cooldown * 0.3f, "three kills with one call give back half the cooldown (" + left.ToString("0.0") + " s left)");
            else
                Check(left > def.Cooldown * 0.8f, "two kills give nothing back (" + left.ToString("0.0") + " s left)");
        }

        // Mastered: two more balls along the aim, staggered.
        reset();
        me.MasteryXp[AbilityId.CannonCall] = 99999f;
        var behind = make(mark - new V2(Game.CcSideGap, 0f), 1e6f);
        var beyond = make(mark + new V2(Game.CcSideGap, 0f), 1e6f);
        hostAbility.Invoke(g, new object[] { 1, 0, mark, false });
        Check(g.Effects.Count(f => f.Kind == FxKind.CannonCall) == 3, "mastered: three balls");
        step(Game.CcLandT + 2f * Game.CcStagger + 0.2f);
        Check(lost(behind) > 0f && lost(beyond) > 0f, "the side balls land behind and beyond the mark");

        // Siege Powder: 25% harder.
        reset();
        var a1 = make(mark, 1e6f);
        hostAbility.Invoke(g, new object[] { 1, 0, mark, false });
        step(Game.CcLandT + 0.1f);
        float plain = lost(a1);
        reset();
        me.Stats.CannonDamage = 0.25f;
        var a2 = make(mark, 1e6f);
        hostAbility.Invoke(g, new object[] { 1, 0, mark, false });
        step(Game.CcLandT + 0.1f);
        Check(Math.Abs(lost(a2) / plain - 1.25f) < 0.03f, "Siege Powder: 25% more damage");

    }
}
