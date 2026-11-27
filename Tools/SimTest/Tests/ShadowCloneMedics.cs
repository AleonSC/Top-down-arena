using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Shadow Clone Medics (ShadowCloneMedics.cs): key 2 of the Healer Ninja's Ultimate row reaching the run; the cast (a clone on
// every player, you included, 10 s, 120 + 6 a wave health, on every screen); each heals its player 4 every 0.5 s; it steps onto
// the line of a shot aimed at its player and stops it (red aimed shots too), never a shot aimed at someone else; broken at 0
// health with its fade heal; a new cast replaces the old clones; it ends on time and between waves; mastered.
static partial class Program
{
    static void ShadowCloneMedicsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags, null,
            new[] { typeof(byte), typeof(V2), typeof(V2), typeof(float), typeof(float), typeof(float), typeof(int), typeof(bool), typeof(float) }, null);
        var stops = typeof(Game).GetMethod("CloneMedicStops", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.ShadowCloneMedics);
        Check(def.Name == "Shadow Clone Medics" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Healer && def.Kit == Kit.Ninja,
            "Shadow Clone Medics is the Healer Ninja's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Ninja) == AbilityId.BlossomStorm && Upgrades.Ultimate(Role.Healer, Kit.Ninja, 1) == AbilityId.AstralProjection
            && Upgrades.Ultimate(Role.Healer, Kit.Ninja, 2) == AbilityId.ShadowCloneMedics,
            "Blossom Storm stays the default and Astral Projection key 1; Shadow Clone Medics is key 2");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Medic", Role.Healer, 0, 0, Kit.Ninja, 0, 2);
        var c1 = new Session();
        c1.Join("127.0.0.1", port, "Tank", Role.Tank, 0, 0, Kit.Soldier, 0, 0);
        var c2 = new Session();
        c2.Join("127.0.0.1", port, "Dps", Role.Dps, 0, 0, Kit.Knight, 0, 0);
        var s = new[] { host, c1, c2 };
        Run(s, dt, 3, () => host.Lobby.Count == 3 && c1.Lobby.Count == 3 && c2.Lobby.Count == 3 && !host.Game.Running);
        Run(s, dt, 2, () => host.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.ShadowCloneMedics, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, c1, dt);
        Run(s, dt, 4, () => c2.Game.Running);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.ShadowCloneMedics, "in the run the caster's ultimate is Shadow Clone Medics");
        var tank = g.Players[c1.Game.MyId];
        var dps = g.Players[c2.Game.MyId];

        // Far apart, so the Healer's aura reaches nobody but them.
        V2 mePos = new V2(500f, 450f), tankPos = new V2(900f, 250f), dpsPos = new V2(900f, 700f);
        var mine = new HashSet<int>();
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)(secs / dt));
            for (int f = 0; f < n; f++)
            {
                g.Enemies.Clear();
                foreach (var id in g.Bullets.Keys.Where(k => !mine.Contains(k)).ToList()) g.Bullets.Remove(id);
                host.Poll(); c1.Poll(); c2.Poll();
                me.Pos = mePos; tank.Pos = tankPos; dps.Pos = dpsPos;
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(1100f, 450f) });
            }
        };
        Func<byte, Player, V2, Bullet> shoot = (kind, at, from) =>
        {
            V2 dir = (at.Pos - from).Normalized();
            var b = (Bullet)spawnBullet.Invoke(g, new object[] { kind, from, dir * 300f, 0f, 3f, 15f, 0, false, 0f });
            mine.Add(b.Id);
            return b;
        };
        Func<Player, Minion> cloneOf = q => g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.CloneMedic && m.LinkTo == q.Id);
        Func<int> clones = () => g.Minions.Values.Count(m => m.Kind == MinionKind.CloneMedic);
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Minions.Clear();
            foreach (var p in new[] { me, tank, dps }) { p.Downed = false; p.Hp = p.MaxHp; p.InvulnT = 0f; }
            me.Pos = mePos; tank.Pos = tankPos; dps.Pos = dpsPos;
            me.UltT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
            g.State = MatchState.Fighting;
        };
        Action cast = () => { me.Slots[Upgrades.UltSlot].Charges = 1; hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false }); };
        float hpFor = Game.ScmHp + Game.ScmHpPerWave * g.Wave;

        // --- The cast ---------------------------------------------------------------------------------------------------
        reset();
        cast();
        hpFor = Game.ScmHp + Game.ScmHpPerWave * g.Wave;
        Check(clones() == 3 && cloneOf(me) != null && cloneOf(tank) != null && cloneOf(dps) != null, "a clone on every player, you included");
        Check(Math.Abs(cloneOf(tank).Hp - hpFor) < 0.01f && Math.Abs(cloneOf(tank).T - Game.ScmTime) < 0.01f && Math.Abs(me.UltT - Game.ScmTime) < 0.01f,
            "each has 120 + 6 a wave health and 10 s");
        step(0.3f);
        Check(c1.Game.Minions.Values.Count(m => m.Kind == MinionKind.CloneMedic) == 3 && c2.Game.Minions.Values.Any(m => m.Kind == MinionKind.CloneMedic && m.LinkTo == dps.Id && m.GuardFrac > 0.99f),
            "every screen sees the clones, their players and their health");
        Check((cloneOf(tank).Pos - tankPos).Length < Game.ScmTrail + 6f, "a clone stays by its player");

        // --- The heal ---------------------------------------------------------------------------------------------------
        tank.Hp = 50f;
        step(1.01f);
        Check(tank.Hp >= 50f + 2f * Game.ScmHeal - 0.01f && tank.Hp <= 50f + 3f * Game.ScmHeal + 0.01f, "it heals its player 4 every 0.5 s (" + (tank.Hp - 50f) + " in a second)");

        // --- Stepping into the shots aimed at its player ----------------------------------------------------------------
        float tankHp = tank.Hp = tank.MaxHp, cloneHp = cloneOf(tank).Hp;
        var shot = shoot(Defs.SmallShot, tank, tankPos + new V2(240f, 0f));
        step(1.2f);
        Check(!g.Bullets.ContainsKey(shot.Id) && tank.Hp == tankHp && cloneOf(tank).Hp < cloneHp - 14f,
            "a shot aimed at its player stops on the clone (the clone took " + (cloneHp - cloneOf(tank).Hp) + ")");
        var clone = cloneOf(tank);
        Check(clone.Pos.X > tankPos.X + 10f, "the clone stepped onto the shot's line");
        cloneHp = clone.Hp;
        var red = shoot(Defs.PierceShot, tank, tankPos + new V2(-240f, 30f));
        step(1.2f);
        Check(!g.Bullets.ContainsKey(red.Id) && tank.Hp == tankHp && clone.Hp < cloneHp - 14f, "red aimed shots too");
        // A shot heading for someone else passes through.
        var stray = new Bullet { Kind = Defs.SmallShot, Pos = clone.Pos, Vel = (clone.Pos - tankPos).Normalized() * 300f, Radius = 5f, Dmg = 15f };  // (flying on, away from its player)
        Check(!(bool)stops.Invoke(g, new object[] { stray }), "a shot aimed at someone else passes through the clone");

        // --- Broken: it pops, and its player heals a fifth of what it soaked -------------------------------------------
        clone.Hp = 1f;
        tank.Hp = 50f;
        float soaked = ((Dictionary<int, float>)typeof(Game).GetField("scmSoaked", flags).GetValue(g))[clone.Id] + 1f;
        float healed0 = tank.Hp;
        shoot(Defs.SmallShot, tank, tankPos + new V2(240f, 0f));
        step(1.2f);
        Check(cloneOf(tank) == null && clones() == 2, "at 0 health the clone is gone, the others stay");
        Check(tank.Hp >= healed0 + soaked * Game.ScmFadeHeal - 0.5f, "its player heals a fifth of what it soaked");
        Check(g.Effects.Any(e => e.Kind == FxKind.CloneMedicPuff), "it goes in a puff of smoke");

        // --- Recast, time, between waves, mastered ----------------------------------------------------------------------
        cast();
        Check(clones() == 3, "a new cast replaces the old clones (three again, not five)");
        step(Game.ScmTime + 0.2f);
        Check(clones() == 0 && c1.Game.Minions.Values.All(m => m.Kind != MinionKind.CloneMedic), "they go on time, on every screen");
        reset();
        cast();
        g.State = MatchState.Intermission;
        typeof(Game).GetMethod("TickMinions", flags).Invoke(g, new object[] { dt });
        Check(clones() == 0, "they go between waves (" + clones() + " left, " + g.State + ")");
        reset();
        me.MasteryXp[AbilityId.ShadowCloneMedics] = 1e6f;
        cast();
        var mm = cloneOf(me);
        hpFor = Game.ScmHp + Game.ScmHpPerWave * g.Wave;
        Check(Math.Abs(mm.T - Game.ScmTimeMastered) < 0.01f && Math.Abs(mm.Hp - hpFor * Game.ScmMasteredHp) < 0.01f, "mastered: 12 s and 40% more health (" + mm.T + " s, " + mm.Hp + " of " + hpFor + ")");
        me.MasteryXp.Remove(AbilityId.ShadowCloneMedics);
        reset();
        Check(def.Bot(new BotSense { AllyHurt = true }) && !def.Bot(new BotSense()), "a bot casts it when someone is hurt or in danger");
        foreach (var x in s) x.Leave("");
    }
}
