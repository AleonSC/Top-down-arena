using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Pressure Point Press (PressurePointPress.cs): key 2 of the Healer Ninja's Ultimate row reaching the run; the Mark (the 8
// nearest in reach held, far ones left alone); a blink beside each in turn (teleports), each pressed for 30, stunned 2 s and
// poisoned, the caster untouchable; each press healing the most hurt teammate; heavies slowed not stunned; the Seal back where
// you cast adding 1 s of stun; a client seeing the effects; going down or the wave ending stops it; mastered (12), Hundred
// Meridians, the bot.
static partial class Program
{
    static void PressurePointPressTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.PressurePointPress);
        Check(def.Name == "Pressure Point Press" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Healer && def.Kit == Kit.Ninja,
            "Pressure Point Press is the Healer Ninja's ultimate on key 2, 70 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Ninja) == AbilityId.BlossomStorm && Upgrades.Ultimate(Role.Healer, Kit.Ninja, 1) == AbilityId.AstralProjection
            && Upgrades.Ultimate(Role.Healer, Kit.Ninja, 2) == AbilityId.PressurePointPress, "Blossom Storm stays the default; key 1 is unchanged");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Press", Role.Healer, 0, 0, Kit.Ninja, 0, 2);
        var c1 = new Session();
        c1.Join("127.0.0.1", port, "Mate", Role.Dps, 0, 0, Kit.Soldier, 0, 0);
        var s = new[] { host, c1 };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && c1.Lobby.Count == 2 && !host.Game.Running);
        Run(s, dt, 2, () => host.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.PressurePointPress, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, c1, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.PressurePointPress, "in the run the caster's ultimate is Pressure Point Press");

        var me = g.Players[1];
        int mateId = c1.Game.MyId;
        var mate = g.Players[mateId];
        V2 mePos = new V2(700f, 500f), aimAt = new V2(1000f, 500f), matePos = new V2(200f, 900f);
        var mine = new HashSet<int>();
        Func<EnemyType, V2, float, Enemy> make = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                g.Bullets.Clear();
                host.Poll(); c1.Poll();
                mate.Pos = matePos;
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        var masteredField = typeof(Game).GetField("castMastered", flags);
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear(); g.PppRuns.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Downed = false; me.Hp = me.MaxHp; me.UltT = 0f; me.InvulnT = 0f; me.PppPresses = 0;
            mate.Downed = false; mate.Hp = mate.MaxHp;
            me.Stats.PppDmgMult = 1f; me.Stats.PppMore = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = me.Disp = mePos; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aimAt, false });
        float press = Game.PppDmg * me.Stats.DmgMult;

        // --- The Mark ----------------------------------------------------------------------------------------------------
        reset();
        var near = new List<Enemy>();
        for (int i = 0; i < 10; i++)  // the 8 nearest are the 150-290 px ones
            near.Add(make(EnemyType.Grunt, mePos + V2.FromAngle(i * MathX.Tau / 10f) * (150f + i * 20f), 1e6f));
        var far = make(EnemyType.Grunt, mePos + new V2(0f, -480f), 1e6f);
        far.FrozenT = 999f;
        mate.Hp = mate.MaxHp * 0.4f;
        cast();
        Check(g.PppRuns.Count == 1 && g.PppRuns[0].Targets.Count == 8, "casting marks the 8 nearest in reach (" + g.PppRuns[0].Targets.Count + ")");
        Check(g.PppRuns[0].Targets.All(id => near.Take(8).Any(e => e.Id == id)), "the marked are the nearest");
        Check(near.Take(8).All(e => e.FrozenT > 0f), "the marked are held where they stand");
        Check(me.InvulnT >= Game.PppEndAt(8) - 0.01f, "you're untouchable for the whole cast");
        Check(g.Effects.Count(x => x.Kind == FxKind.PressurePoint && x.Radius == Game.PppFxTarget) == 8 && g.Effects.Any(x => x.Kind == FxKind.PressurePoint && x.Radius == Game.PppFxMark),
            "the Mark ring and a reticle on each marked enemy");
        step(0.2f);
        Check(c1.Game.Effects.Any(x => x.Kind == FxKind.PressurePoint), "other games see the Mark");
        Check(near.All(e => e.Hp == e.MaxHp), "nothing lands during the Mark");

        // --- The presses -------------------------------------------------------------------------------------------------
        step(Game.PppMark - 0.2f + 0.02f);
        Check(me.PppPresses == 1 && me.Pos.DistanceTo(Enemies(g, g.PppRuns[0].Targets[0]).Pos) < 60f, "the first press blinks you beside the first target");
        float hp0 = mate.Hp;
        step(Game.PppHop * 7f);
        Check(me.PppPresses == 8, "one press each, " + Game.PppHop + " s apart (" + me.PppPresses + ")");
        Check(near.Take(8).All(e => Math.Abs(e.MaxHp - e.Hp - press) < press * 0.5f + 8f * 2f), "each press hits for 30 (plus poison)");
        Check(near.Take(8).All(e => e.StunT > 0.9f && e.PoisonT > 3.5f && e.PoisonDps > 0f), "each pressed enemy is stunned and poisoned");
        Check(near.Skip(8).All(e => e.Hp == e.MaxHp) && far.Hp == far.MaxHp, "the unmarked are left alone");
        Check(mate.Hp > hp0 && g.Effects.Count(x => x.Kind == FxKind.PressurePoint && x.Radius == Game.PppFxWisp) >= 7, "each press sends a wisp healing the most hurt teammate");
        Check(g.Effects.Count(x => x.Kind == FxKind.PressurePoint && x.Radius == Game.PppFxDash) >= 7, "a dash streak for each blink");

        // --- The Seal ----------------------------------------------------------------------------------------------------
        float stunBefore = near[0].StunT;
        step(Game.PppHop + 0.02f);
        Check(me.Pos.DistanceTo(mePos) < 5f, "the Seal blinks you back where you cast");
        Check(near[0].StunT > stunBefore + Game.PppSealStun * 0.8f, "and adds a second of stun to the pressed");
        Check(g.Effects.Count(x => x.Kind == FxKind.PressurePoint && x.Radius == Game.PppFxSealLine) == 8, "a seal thread to each pressed enemy");
        step(Game.PppSealHold + 0.05f);
        Check(g.PppRuns.Count == 0, "the cast is over after the Seal");
        float poisoned = near[0].Hp;
        step(1f);
        Check(near[0].Hp < poisoned - Game.PppPoisonDps * me.Stats.DmgMult * 0.8f, "the poison ticks about 8 a second");

        // --- Heavies are slowed, not stunned ------------------------------------------------------------------------------
        reset();
        var hv = make(EnemyType.Gatekeeper, mePos + new V2(200f, 0f), 1e6f);
        cast();
        step(Game.PppMark + 0.05f);
        Check(hv.StunT <= 0f && hv.SlowT > 1f && Math.Abs(hv.SlowMult - Game.PppHeavySlow) < 0.01f && hv.PoisonT > 0f, "a heavy is slowed and poisoned, not stunned");
        step(1f);

        // --- Going down or the wave ending stops it ------------------------------------------------------------------------
        reset();
        for (int i = 0; i < 6; i++) make(EnemyType.Grunt, mePos + V2.FromAngle(i) * 200f, 1e6f);
        cast();
        step(Game.PppMark + 0.05f);
        me.Downed = true;
        step(dt);
        Check(g.PppRuns.Count == 0, "going down stops it");
        reset();
        make(EnemyType.Grunt, mePos + new V2(250f, 0f), 1e6f);
        cast();
        g.State = MatchState.Intermission;
        typeof(Game).GetMethod("TickPressurePoint", flags).Invoke(g, new object[] { dt });
        Check(g.PppRuns.Count == 0, "the wave ending stops it");
        g.State = MatchState.Fighting;

        // --- Nobody in reach: you blink nowhere -----------------------------------------------------------------------------
        reset();
        cast();
        step(Game.PppEndAt(0) + 0.05f);
        Check(me.PppPresses == 0 && g.PppRuns.Count == 0 && me.Pos.DistanceTo(mePos) < 5f, "with nobody in reach it just rings and ends");

        // --- Mastered, Hundred Meridians, the bot ---------------------------------------------------------------------------
        reset();
        for (int i = 0; i < 16; i++) make(EnemyType.Grunt, mePos + V2.FromAngle(i * MathX.Tau / 16f) * (120f + (i % 4) * 50f), 1e6f);
        me.MasteryXp[AbilityId.PressurePointPress] = 1e6f;
        cast();
        Check(g.PppRuns[0].Targets.Count == Game.PppMaxMastered, "mastered: marks 12");
        me.MasteryXp.Remove(AbilityId.PressurePointPress);
        step(Game.PppEndAt(Game.PppMaxMastered) + 0.1f);
        reset();
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Hundred Meridians");
        Check(card != null && card.RequiresAbility == AbilityId.PressurePointPress && card.Rarity == Rarity.Rare, "Hundred Meridians is a Rare card for Pressure Point Press");
        card.Apply(me.Stats);
        for (int i = 0; i < 12; i++) make(EnemyType.Grunt, mePos + V2.FromAngle(i * MathX.Tau / 12f) * (150f + (i % 3) * 50f), 1e6f);
        var b1 = g.Enemies.Values.OrderBy(e => e.Pos.DistanceSq(mePos)).First();
        cast();
        Check(g.PppRuns[0].Targets.Count == 10, "Hundred Meridians: 2 more marks");
        step(Game.PppMark + 0.02f);
        Check(Math.Abs((b1.MaxHp - b1.Hp) - press * 1.2f) < 2f, "and presses 20% harder (" + (b1.MaxHp - b1.Hp).ToString("0.0") + ")");
        step(Game.PppEndAt(10));
        reset();
        Check(def.Bot(new BotSense { NearEnemies = 3 }) && !def.Bot(new BotSense { NearEnemies = 2 }), "a bot casts it into a crowd");
    }

    static Enemy Enemies(Game g, int id) => g.Enemies[id];
}
