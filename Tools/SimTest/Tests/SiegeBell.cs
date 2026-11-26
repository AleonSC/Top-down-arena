using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Siege Bell (SiegeBell.cs), the Tank Knight's 4th choice on the lobby's 4th key: the table and cards, the pick reaching the
// run and both screens, the real key path and the sound, the caster braced (rooted, 25% less damage) while it rings, tolls 1
// and 2 (8 and a flinch, only within 120 px), the third toll's shockwave reaching near before far (20, a shove, a 2.5 s stun;
// heavy enemies less), nothing past 260 px, mastery (3.5 s stun, enemy shots erased) and Great Bell (+25% reach).
static partial class Program
{
    static void SiegeBellTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("BellCut", flags);

        var def = Upgrades.Ability(AbilityId.SiegeBell);
        Check(def.Name == "Siege Bell" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Knight && def.Role == Role.Tank && def.Cooldown == 22f,
            "Siege Bell: a 22 s Tank Knight 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Tank, Kit.Knight) == 4 && Upgrades.FirstAbility(Role.Tank, Kit.Knight, 3) == AbilityId.SiegeBell
            && Upgrades.FirstAbility(Role.Tank, Kit.Knight, 0) == AbilityId.ShieldWall && Upgrades.FirstAbility(Role.Tank, Kit.Knight, 2) == AbilityId.CavalryCall,
            "the Tank Knight's 4th key is Siege Bell; its first three are unchanged");
        Check(Upgrades.All.Any(u => u.ClassAbility && u.Grants == AbilityId.SiegeBell) && Upgrades.All.Any(u => u.Name == "Great Bell" && u.RequiresAbility == AbilityId.SiegeBell && u.Rarity == Rarity.Rare),
            "Siege Bell is a card for Tank Knights who picked another, and Great Bell a Rare card for it");

        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Ringer", Role.Tank, 0, 3, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[0].Id == AbilityId.SiegeBell && client.Game.Players[1].Slots[0].Id == AbilityId.SiegeBell, "the 4th pick reaches the run, on both screens");
        g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f), aim = home + new V2(100f, 0f);
        V2 bell = home + new V2(Game.BellAhead, 0f);
        var mine = new HashSet<int>();
        var hold = new Dictionary<int, V2>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; e.Pos = at; mine.Add(e.Id); hold[e.Id] = at; return e;
        };
        bool pin = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; if (pin && hold.ContainsKey(e.Id)) e.Pos = hold[e.Id]; }
                hostS.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
                g.Update(dt); client.Game.Update(dt);  // (effects age here, as on a real screen: the brace reads the bell's effect)
            }
        };
        Func<Enemy, float> lost = e => e.MaxHp - e.Hp;
        Action reset = () =>
        {
            mine.Clear(); hold.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); client.Game.Effects.Clear();
            pin = true;
            step(Game.BellFxDur(Game.BellStunMastered + 1f));  // any bell from before is over
            me.Slots[0].Charges = 1; me.Slots[0].Timer = 0f; me.Pos = home; me.Aim = 0f; me.Downed = false; me.Hp = me.MaxHp;
            me.MasteryXp.Remove(AbilityId.SiegeBell); me.Stats.BellRadiusMult = 1f; me.Stats.BellStunBonus = 0f;
        };
        var heavyType = HeavyFoe();

        // The real key path: a press casts it, with its sound, on both screens.
        reset();
        var near = make(bell + new V2(60f, 0f), EnemyType.Grunt);
        var mid = make(bell + new V2(0f, 180f), EnemyType.Grunt);
        var far = make(bell + new V2(-240f, 0f), EnemyType.Grunt);
        var outside = make(bell + new V2(0f, -320f), EnemyType.Grunt);
        var heavy = make(bell + new V2(0f, -150f), heavyType);
        step(dt);
        foreach (var e in new[] { near, mid, far, outside, heavy }) e.Hp = e.MaxHp;
        g.Sounds.Clear();
        hostS.Poll(); client.Poll();
        g.FixedStep(dt, new PlayerInput { Ability = true, AimAt = aim });
        g.Update(dt); client.Game.Update(dt);
        Check(g.Effects.Count(f => f.Kind == FxKind.SiegeBell) == 1, "pressing the ability key casts it: one bell effect");
        Check(g.Sounds.Any(c => c.Sfx == Sfx.SiegeBell), "the tolls sound (Sfx.SiegeBell, sent with the cast)");
        var fx = g.Effects.First(f => f.Kind == FxKind.SiegeBell);
        Check(fx.Pos.DistanceTo(bell) < 2f && Math.Abs(fx.Radius - Game.BellRadius) < 0.1f, "the bell comes down 50 px ahead, reaching 260 px");
        step(0.2f);
        Check(client.Game.Effects.Count(f => f.Kind == FxKind.SiegeBell) == 1, "the other screen sees the bell");
        Check(lost(near) == 0f, "nothing lands before the first toll");
        Check(g.BellRooted(me) && client.Game.BellRooted(client.Game.Players[1]), "the caster stands braced while it rings (both screens know)");
        Check(Math.Abs((float)damage.Invoke(g, new object[] { me }) - 0.25f) < 0.001f, "braced: 25% less damage");
        V2 was = me.Pos;
        for (int f = 0; f < 0.3f / dt; f++) { hostS.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), AimAt = aim }); g.Update(dt); client.Game.Update(dt); }
        Check(me.Pos.DistanceTo(was) < 1f, "braced: the caster can't walk (" + me.Pos.DistanceTo(was).ToString("0.0") + " px)");
        me.Pos = home;
        // 0.5 s in: toll 1 has rung.
        Check(lost(near) >= 8f && lost(near) < 12f, "toll 1: 8 to an enemy within 120 px (" + lost(near).ToString("0") + ")");
        Check(lost(mid) == 0f && lost(far) == 0f, "toll 1 reaches only 120 px");
        step(0.4f);  // 0.9 s: toll 2
        Check(lost(near) >= 16f && lost(near) < 24f, "toll 2: another 8 (" + lost(near).ToString("0") + ")");
        step(0.45f);  // 1.35 s: toll 3 has rung, the front is just out
        Check(lost(near) >= 36f && near.StunT > 2f, "toll 3: the wave reaches the near enemy first: 20 and a stun (" + lost(near).ToString("0") + ", " + near.StunT.ToString("0.00") + " s)");
        Check(lost(far) == 0f, "the far enemy isn't reached yet: the wave races outward");
        step(0.5f);
        Check(lost(mid) >= 20f && lost(far) >= 20f && far.StunT > 2f, "the wave reaches 240 px out (" + lost(far).ToString("0") + ")");
        Check(lost(outside) == 0f && outside.StunT <= 0f, "nothing past 260 px");
        Check(heavy.StunT > 0f && heavy.StunT < Game.BellStunHeavy + 0.01f && near.StunT > heavy.StunT, "a heavy enemy is stunned for less (" + heavy.StunT.ToString("0.00") + " s)");
        Check(!g.BellRooted(me) && (float)damage.Invoke(g, new object[] { me }) == 0f, "the brace ends after the third toll (" + g.BellRooted(me) + ", " + damage.Invoke(g, new object[] { me }) + ", " + g.Effects.Where(f => f.Kind == FxKind.SiegeBell).Select(f => f.T.ToString("0.00")).FirstOrDefault() + ")");

        // The shove: an enemy left free is pushed out.
        reset();
        var free = make(bell + new V2(100f, 0f), EnemyType.Grunt);
        hold.Remove(free.Id);
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(Game.BellTolls[2] - 0.03f);
        float before = free.Pos.DistanceTo(bell);
        step(0.4f);
        Check(free.Pos.DistanceTo(bell) > before + 15f, "the shockwave shoves enemies out (" + before.ToString("0") + " to " + free.Pos.DistanceTo(bell).ToString("0") + " px)");

        // Mastered: 3.5 s, and enemy shots in the wave's path are erased.
        reset();
        me.MasteryXp[AbilityId.SiegeBell] = 99999f;
        var m = make(bell + new V2(80f, 0f), EnemyType.Grunt);
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(Game.BellTolls[2] - 0.05f);
        int shot = 95101, distant = 95102;
        g.Bullets[shot] = new Bullet { Id = shot, Kind = Defs.SmallShot, Pos = bell + new V2(0f, 150f), Radius = 4f, Life = 5f, Vel = new V2(1f, 0f), Dmg = 10f };
        g.Bullets[distant] = new Bullet { Id = distant, Kind = Defs.SmallShot, Pos = bell + new V2(0f, -320f), Radius = 4f, Life = 5f, Vel = new V2(1f, 0f), Dmg = 10f };
        step(0.25f);
        Check(m.StunT > 3f, "mastered: a 3.5 s stun (" + m.StunT.ToString("0.00") + " s)");
        step(0.4f);
        Check(!g.Bullets.ContainsKey(shot) && g.Bullets.ContainsKey(distant), "mastered: the wave erases an enemy shot it passes, not one beyond it");

        // Great Bell: 25% farther, 0.5 s longer.
        reset();
        me.Stats.BellRadiusMult = 1.25f; me.Stats.BellStunBonus = 0.5f;
        var edge = make(bell + new V2(300f, 0f), EnemyType.Grunt);
        step(dt); edge.Hp = edge.MaxHp;
        hostAbility.Invoke(g, new object[] { 1, 0, aim, false });
        step(Game.BellTolls[2] + Game.BellWave + 0.1f);
        Check(lost(edge) >= 20f && edge.StunT > 2.4f, "Great Bell: the wave reaches an enemy 300 px out and stuns it 3 s");
    }
}
