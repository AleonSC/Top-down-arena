using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Mammoth Fall (MammothFall.cs): the DPS Summoner's key 2 on the lobby's Ultimate row (the pick reaching the run), the aim ring
// following the cursor while it forms (kept within range), the client seeing it, the slam (the crush under it, the shockwave's
// damage and stun, nothing outside it, heavies not shoved), the rampage trampling enemies near the crater and never one out of
// its reach, the end on time, going down while it forms, mastered, Ice Age.
static partial class Program
{
    static void MammothFallTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.MammothFall);
        Check(def.Name == "Mammoth Fall" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Kit == Kit.Summoner && def.Role == Role.Dps && def.Capstone != null,
            "Mammoth Fall is the DPS Summoner's ultimate on key 2, 75 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Summoner) == AbilityId.Legion && Upgrades.Ultimate(Role.Dps, Kit.Summoner, 1) == AbilityId.RodentMinigun
            && Upgrades.Ultimate(Role.Dps, Kit.Summoner, 2) == AbilityId.MammothFall,
            "Legion stays the default, key 1 Rodent Minigun, key 2 Mammoth Fall");
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Ice Age");
        Check(card != null && card.RequiresAbility == AbilityId.MammothFall && card.Rarity == Rarity.Rare, "its Rare card is Ice Age");

        // --- Into a run -----------------------------------------------------------------------------------------------------
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Tusker", Role.Dps, 0, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Summoner);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.MammothFall, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.MammothFall && client.Game.Players[1].Slots[U].Id == AbilityId.MammothFall, "in the match the host's F is Mammoth Fall, on every game");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (where, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, where, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = where; return e;
        };
        V2 mePos = new V2(300f, 450f), aim = new V2(700f, 450f);
        bool pinned = true;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                if (pinned)
                    foreach (var kv in spots) { kv.Key.Pos = kv.Value; kv.Key.KnockLeft = V2.Zero; }
                foreach (var e in spots.Keys) e.FireT = 99f;
                g.Minions.Clear();  // (the wolves would bite the pinned enemies)
                me.Pos = mePos;
                me.Hp = me.MaxHp;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
            }
        };
        Func<Zone> mammoth = () => g.Zones.Values.FirstOrDefault(zz => zz.Kind == ZoneKind.Mammoth);
        Action reset = () =>
        {
            g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); spots.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            me.UltT = 0f; me.Downed = false; me.MammothTramples = 0; me.Pos = mePos; me.Aim = 0f;
            me.Slots[U].Charges = 1; me.Slots[U].Timer = 0f;
            masteredField.SetValue(g, false);
            aim = new V2(700f, 450f); pinned = true;
            g.Players[2].Pos = new V2(300f, 900f);
            g.State = MatchState.Fighting;
        };

        // --- The summon: the ring follows the aim, within range --------------------------------------------------------------
        reset();
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        var z = mammoth();
        float total = Game.MfTotal(Game.MfRampage);
        Check(z != null && Math.Abs(z.Duration - total) < 1e-3f && Math.Abs(me.UltT - total) < 1e-3f, "casting summons it: 1.6 + 0.5 + 0.35 + 3 + 0.8 s");
        Check(z != null && z.Origin.CloserThan(aim, 1f) && Math.Abs(z.Radius - Game.MfWave) < 0.01f, "the ring starts at the cursor, the shockwave 200 px");
        aim = new V2(1400f, 450f);
        step(0.5f);
        Console.WriteLine("  mammoth fall: aiming 1100 px away puts the ring {0:0} px out", (z.Origin - mePos).Length);
        Check(Math.Abs((z.Origin - mePos).Length - Game.MfRange) < 2f, "the ring follows the cursor but stays within 520 px");
        Check(z.Pos.CloserThan(mePos, 1f), "it hangs over you while it forms");
        step(0.1f);
        var cz = client.Game.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.Mammoth);
        Check(cz != null && cz.Origin.CloserThan(z.Origin, 2f), "the client sees it and where it will land");
        aim = new V2(700f, 300f);
        step(Game.MfSummon - 0.6f + 0.05f);
        V2 lands = z.Origin;
        aim = new V2(400f, 700f);  // too late: it has launched
        step(0.2f);
        Check(z.Origin.CloserThan(lands, 1f) && lands.CloserThan(new V2(700f, 300f), 1f), "where you aim at the end of the summon is where it lands; aiming after does nothing");

        // --- The slam ----------------------------------------------------------------------------------------------------------
        reset();
        V2 at = new V2(700f, 450f);
        var under = pin(at + new V2(20f, 0f), EnemyType.Grunt);
        var inWave = pin(at + new V2(0f, 150f), EnemyType.Grunt);
        var outside = pin(at + new V2(0f, -280f), EnemyType.Grunt);
        var heavyType = HeavyFoe(moving: true);
        var heavy = pin(at + new V2(-150f, 0f), heavyType);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = mammoth();
        step(Game.MfImpactAt - 0.1f);
        Check(under.Hp == under.MaxHp && inWave.Hp == inWave.MaxHp, "nothing is hurt before it lands");
        pinned = false;
        step(0.1f + dt * 2);
        float lostUnder = under.MaxHp - under.Hp, lostWave = inWave.MaxHp - inWave.Hp;
        Console.WriteLine("  mammoth fall: the slam took {0:0} under it, {1:0} in the shockwave (x{2:0.00}); stuns {3:0.00} s", lostUnder, lostWave, z.DmgMult, inWave.StunT);
        Check(z.Leg == 0 && z.Pos.CloserThan(at, 1f), "it lands at the ring");
        Check(Math.Abs(lostUnder - Game.MfCrushDmg * z.DmgMult) < 0.2f * Game.MfCrushDmg * z.DmgMult, "the one under it is crushed (120)");
        Check(Math.Abs(lostWave - Game.MfWaveDmg * z.DmgMult) < 0.2f * Game.MfWaveDmg * z.DmgMult, "the shockwave hits for 25");
        Check(inWave.StunT > Game.MfStun - 0.1f && heavy.StunT > Game.MfStun - 0.1f && under.StunT > Game.MfStun - 0.1f, "everything in the shockwave is stunned 1.5 s, heavies too");
        Check(outside.Hp == outside.MaxHp && outside.StunT <= 0f, "nothing outside it is touched");
        Check(inWave.KnockLeft.Length > 1f && heavy.KnockLeft.Length < 0.01f, "the shockwave shoves grunts out, not heavies");

        // --- The rampage ---------------------------------------------------------------------------------------------------------
        reset();
        var near1 = pin(at + new V2(150f, 120f), EnemyType.Grunt);
        var near2 = pin(at + new V2(-160f, -100f), EnemyType.Grunt);
        var near3 = pin(at + new V2(30f, -170f), EnemyType.Grunt);
        var far = pin(at + new V2(0f, 380f), EnemyType.Grunt);  // out of its reach from the crater
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = mammoth();
        step(Game.MfImpactAt + Game.MfSettle + 0.1f);
        float n1 = near1.Hp, n2 = near2.Hp;
        bool sawPrey = false;
        for (int i = 0; i < (int)(Game.MfRampage / 0.1f); i++)
        {
            step(0.1f);
            sawPrey |= z.Side == near1.Id || z.Side == near2.Id;
        }
        Console.WriteLine("  mammoth fall: the rampage trampled {0} times ({1:0} and {2:0} off the two near the crater)", me.MammothTramples, n1 - near1.Hp, n2 - near2.Hp);
        Check(sawPrey && n1 > near1.Hp && n2 > near2.Hp, "it charges the enemies near the crater and tramples both");
        Check(me.MammothTramples >= 4 && near3.Hp < near3.MaxHp, "and keeps turning on the next one (" + me.MammothTramples + " tramples)");
        Check(far.Hp == far.MaxHp, "it never goes after one more than 300 px from the crater");
        step(Game.MfLeave + 0.2f);
        Check(mammoth() == null && me.UltT <= 0.01f, "it has gone on time");

        // --- Going down while it forms --------------------------------------------------------------------------------------
        reset();
        var victim = pin(at, EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        step(0.5f);
        me.Downed = true;
        step(dt * 2);
        me.Downed = false;
        step(Game.MfImpactAt);
        Check(mammoth() == null && victim.Hp == victim.MaxHp, "going down while it forms cancels it");

        // --- Mastered, and Ice Age ------------------------------------------------------------------------------------------------
        reset();
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, aim });
        z = mammoth();
        Check(z != null && Math.Abs(z.Duration - Game.MfTotal(Game.MfRampageMastered)) < 1e-3f && Game.MfMastered(z), "mastered: it rampages 4.5 s");
        reset();
        card.Apply(me.Stats);
        var edge = pin(at + new V2(0f, 230f), EnemyType.Grunt);
        step(dt);
        cast.Invoke(g, new object[] { me, U, aim });
        z = mammoth();
        step(Game.MfImpactAt - 0.05f);
        pinned = false;
        step(0.05f + dt * 2);
        Console.WriteLine("  mammoth fall, Ice Age: shockwave {0:0} px, the grunt 230 px out lost {1:0} and is stunned {2:0.00} s", z.Radius, edge.MaxHp - edge.Hp, edge.StunT);
        Check(Math.Abs(z.Radius - 250f) < 0.5f && edge.Hp < edge.MaxHp && edge.StunT > Game.MfStun + 0.4f, "Ice Age: the shockwave reaches 250 px and stuns 2 s");
    }
}
