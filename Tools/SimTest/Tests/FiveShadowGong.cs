using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Five Shadow Gong (FiveShadowGong.cs): key 3 of the Tank Ninja's Ultimate row reaching the run; the gong and the strike;
// enemies in reach stunned (not heavies), far ones left alone; 5 shadows x 3 cuts on the 15 nearest, nothing landing before the
// sheathe and every cut at once on it (120 each, heavies 60); a client seeing the effects; fewer enemies taking the spare cuts;
// the 5 smoke clouds slowing enemies' moves and shots; the wave ending stops it; mastered (4 cuts each), Temple Bronze, the bot.
static partial class Program
{
    static void FiveShadowGongTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        // --- The lobby: key 3 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.FiveShadowGong);
        Check(def.Name == "Five Shadow Gong" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Tank && def.Kit == Kit.Ninja,
            "Five Shadow Gong is the Tank Ninja's ultimate on key 3, 70 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Ninja) == AbilityId.ThousandClones && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 1) == AbilityId.DragonsDogma
            && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 2) == AbilityId.SpiritAvatar && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 3) == AbilityId.FiveShadowGong,
            "Thousand Clones stays the default; keys 1 and 2 are unchanged");

        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 3 };
        host.Host(port, "Gong", Role.Tank, 0, 0, Kit.Ninja, 0, 3);
        var c1 = new Session();
        c1.Join("127.0.0.1", port, "Mate", Role.Dps, 0, 0, Kit.Soldier, 0, 0);
        var s = new[] { host, c1 };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && c1.Lobby.Count == 2 && !host.Game.Running);
        Run(s, dt, 2, () => host.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.FiveShadowGong, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, c1, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.FiveShadowGong, "in the run the caster's ultimate is Five Shadow Gong");

        var me = g.Players[1];
        int mateId = c1.Game.MyId;
        var mate = g.Players[mateId];
        g.DevSetGod(1, true); g.DevSetGod(mateId, true);
        V2 mePos = new V2(700f, 500f), aimAt = new V2(1000f, 500f);
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
                me.Pos = mePos; mate.Pos = new V2(200f, 900f);
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt });
            }
        };
        var masteredField = typeof(Game).GetField("castMastered", flags);
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear(); g.FsgRuns.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Downed = false; me.Hp = me.MaxHp; me.UltT = 0f; me.FsgCuts = me.FsgKills = 0;
            me.Stats.FsgDmgMult = 1f; me.Stats.FsgSmokeMult = 1f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aimAt, false });
        Func<Enemy, bool> alive = e => g.Enemies.ContainsKey(e.Id) && e.Hp > 0f;
        float cut = Game.FsgCut * me.Stats.DmgMult;

        // --- The cast and the strike ------------------------------------------------------------------------------------
        reset();
        var near = new List<Enemy>();
        for (int i = 0; i < 20; i++)
        {
            float ang = i * MathX.Tau / 20f, dist = 200f + (i % 4) * 60f;  // the 15 nearest are the 200-320 px rings
            near.Add(make(EnemyType.Grunt, mePos + V2.FromAngle(ang) * (i % 4 == 3 ? 560f : dist), cut * 0.9f));
        }
        var far = make(EnemyType.Grunt, new V2(1570f, 870f), 1e6f);  // beyond the reach (945 px)
        far.FrozenT = 999f;  // (held there)
        var heavy = make(EnemyType.Gatekeeper, mePos + new V2(0f, 600f), 1e6f);
        cast();
        Check(g.FsgRuns.Count == 1 && g.Effects.Any(x => x.Kind == FxKind.GongShow), "casting drops the gong (GongShow)");
        step(0.3f);
        Check(c1.Game.Effects.Any(x => x.Kind == FxKind.GongShow), "other games see the gong");
        step(Game.FsgStrike - 0.3f + 0.05f);
        Check(near.All(e => e.StunT > 0f), "the strike stuns every enemy in reach");
        Check(far.StunT <= 0f, "an enemy beyond the reach isn't stunned");
        Check(heavy.StunT <= 0f, "a heavy isn't stunned");

        // --- The cuts and the sheathe -----------------------------------------------------------------------------------
        float sheathe = Game.FsgSheatheAt(Game.FsgPer);
        step(sheathe - Game.FsgStrike - 0.1f);
        Check(me.FsgCuts == 15, "five shadows cut three each (" + me.FsgCuts + ")");
        Check(near.All(alive) && near.All(e => e.Hp == e.MaxHp), "nothing lands before the sheathe");
        Check(g.Effects.Count(x => x.Kind == FxKind.GongCut) == 20, "a GongCut for each shadow appearing and each cut");
        Check(c1.Game.Effects.Count(x => x.Kind == FxKind.GongCut) >= 18, "other games see the shadows and cuts");
        step(0.2f);
        int dead = near.Count(e => !alive(e));
        Check(dead == 15 && me.FsgKills == 15, "every cut lands at once on the sheathe (" + dead + " died)");
        Check(near.Where(alive).All(e => e.Pos.DistanceTo(mePos) > 400f), "the 15 nearest were the ones cut");
        Check(far.Hp == far.MaxHp, "the far one is left alone");
        Check(g.Effects.Count(x => x.Kind == FxKind.GongSplit) == 15, "a GongSplit for each enemy cut");

        // --- The smoke ----------------------------------------------------------------------------------------------------
        step(Game.FsgDepartAt(Game.FsgPer) - sheathe + 0.05f);
        var clouds = g.Zones.Values.Where(z => z.Kind == ZoneKind.GongSmoke).ToList();
        Check(clouds.Count == 5 && clouds.All(z => Math.Abs(z.Radius - Game.FsgSmokeR) < 0.01f), "each shadow drops a smoke cloud as it leaves");
        step(Game.FsgPop + 0.1f);
        Check(g.FsgRuns.Count == 0, "the cast is over once the shadows are gone");
        var walker = make(EnemyType.Grunt, clouds[0].Pos, 1e6f);
        step(0.2f);
        Check(walker.FsgSmokeT > 0f && walker.SlowT > 0f && Math.Abs(walker.SlowMult - (1f - Game.FsgSmokeSlow)) < 0.01f, "an enemy in the smoke is slowed 40%");
        walker.FireT = 10f;
        float f0 = walker.FireT;
        step(1f);
        float rate = (f0 - walker.FireT) / 1f;
        var outside = make(EnemyType.Grunt, mePos + new V2(0f, -420f), 1e6f);
        step(0.1f);
        outside.FireT = 10f; float o0 = outside.FireT;
        step(1f);
        float rateOut = (o0 - outside.FireT) / 1f;
        Check(rateOut > 0f && Math.Abs(rate / rateOut - (1f - Game.FsgSmokeSlow)) < 0.06f, "and it shoots 40% slower (" + (rate / rateOut).ToString("0.00") + ")");

        // --- Fewer enemies: the spare cuts go round them again; heavies take half ---------------------------------------
        reset();
        var a1 = make(EnemyType.Grunt, mePos + new V2(250f, 0f), 1e6f);
        var a2 = make(EnemyType.Grunt, mePos + new V2(-250f, 50f), 1e6f);
        var hv = make(EnemyType.Gatekeeper, mePos + new V2(0f, 300f), 1e6f);
        cast();
        step(Game.FsgEndAt(Game.FsgPer) + 0.1f);
        float dealt = (a1.MaxHp - a1.Hp) + (a2.MaxHp - a2.Hp), dealtHeavy = hv.MaxHp - hv.Hp;
        Check(me.FsgCuts == 15, "with three enemies all 15 cuts still land");
        Check(Math.Abs(dealt / cut + dealtHeavy / (Game.FsgHeavyCut * me.Stats.DmgMult) - 15f) < 0.05f && dealtHeavy > 0f,
            "they share the cuts, 120 each and 60 on the heavy (" + (dealt / cut).ToString("0.0") + " + " + (dealtHeavy / (Game.FsgHeavyCut * me.Stats.DmgMult)).ToString("0.0") + ")");

        // --- No enemies: shadows still come and go ----------------------------------------------------------------------
        reset();
        cast();
        step(Game.FsgEndAt(Game.FsgPer) + 0.1f);
        Check(me.FsgCuts == 0 && g.Zones.Values.Count(z => z.Kind == ZoneKind.GongSmoke) == 5 && g.FsgRuns.Count == 0, "with nobody in reach the shadows just leave their smoke");

        // --- The wave ending stops it ------------------------------------------------------------------------------------
        reset();
        make(EnemyType.Grunt, mePos + new V2(250f, 0f), 1e6f);
        cast();
        step(1.4f);
        g.State = MatchState.Intermission;
        typeof(Game).GetMethod("TickShadowGong", flags).Invoke(g, new object[] { dt });
        Check(g.FsgRuns.Count == 0, "the wave ending stops it");
        g.State = MatchState.Fighting;

        // --- Mastered, Temple Bronze, the bot ----------------------------------------------------------------------------
        reset();
        for (int i = 0; i < 24; i++) make(EnemyType.Grunt, mePos + V2.FromAngle(i * MathX.Tau / 24f) * (220f + (i % 3) * 70f), 1e6f);
        me.MasteryXp[AbilityId.FiveShadowGong] = 1e6f;
        cast();
        step(Game.FsgEndAt(Game.FsgPerMastered) + 0.1f);
        Check(me.FsgCuts == 20, "mastered: each shadow cuts 4 (" + me.FsgCuts + ")");
        me.MasteryXp.Remove(AbilityId.FiveShadowGong);
        reset();
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Temple Bronze");
        Check(card != null && card.RequiresAbility == AbilityId.FiveShadowGong && card.Rarity == Rarity.Rare, "Temple Bronze is a Rare card for Five Shadow Gong");
        card.Apply(me.Stats);
        var b1 = make(EnemyType.Grunt, mePos + new V2(250f, 0f), 1e6f);
        cast();
        step(Game.FsgDepartAt(Game.FsgPer) + 0.05f);
        Check(Math.Abs((b1.MaxHp - b1.Hp) / (15f * cut) - 1.2f) < 0.01f, "Temple Bronze: cuts 20% harder");
        Check(g.Zones.Values.Where(z => z.Kind == ZoneKind.GongSmoke).All(z => Math.Abs(z.Radius - Game.FsgSmokeR * 1.25f) < 0.01f), "and the smoke 25% wider");
        reset();
        Check(def.Bot(new BotSense { NearEnemies = Game.FsgBotCrowd }) && !def.Bot(new BotSense { NearEnemies = 2 }), "a bot casts it into a crowd");

        foreach (var x in s) x.Leave("");
    }
}
