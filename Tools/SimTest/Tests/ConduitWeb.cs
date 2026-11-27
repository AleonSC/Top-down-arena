using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Conduit Web (ConduitWeb.cs): key 3 of the Healer Elementalist's Ultimate row reaching the run; the circuit's rule (every
// enemy in reach, three arms from the hand, each joining the closest point already in it); the cast and the field bursting
// out; enemies in the field wired, hurt on the wire, held stunned (not bosses) and shocked each pulse, harder the longer they're
// wired; one walking in is wired too, one outside isn't; teammates in the field heal each pulse; the stun after it ends; going
// down; mastered; the bot's rule; other screens seeing it.
static partial class Program
{
    static void ConduitWebTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 3 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.ConduitWeb);
        Check(def.Name == "Conduit Web" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Healer && def.Kit == Kit.Elementalist,
            "Conduit Web is the Healer Elementalist's ultimate on key 3, 80 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 3) == AbilityId.ConduitWeb && Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 2) == AbilityId.EyeOfCalm
            && Upgrades.Ultimate(Role.Healer, Kit.Elementalist, 1) == AbilityId.StaticBubble && Upgrades.Ultimate(Role.Healer, Kit.Elementalist) == AbilityId.Sanctum,
            "key 3 is Conduit Web; Eye of Calm, Static Bubble and Sanctum keep theirs");

        // --- The circuit's rule (no game needed) -------------------------------------------------------------------------
        {
            var es = new List<Enemy>();
            Func<float, float, Enemy> at = (x, y) => { var e = new Enemy { Id = es.Count + 1, Pos = new V2(x, y), Radius = 10f }; es.Add(e); return e; };
            // six spokes of two enemies each round the hand, and one far out
            for (int i = 0; i < 6; i++)
            {
                V2 d = V2.FromAngle(MathX.Tau * i / 6f);
                at(d.X * 100f, d.Y * 100f);
                at(d.X * 180f, d.Y * 180f);
            }
            var far = at(600f, 0f);
            var into = new List<Enemy>();
            var from = new List<int>();
            Game.CwCircuit(new V2(0f, 0f), new V2(0f, 0f), 330f, es, e => e.Pos, e => true, 3, into, from);
            Check(into.Count == 12 && !into.Contains(far), "every enemy in reach is wired, none outside it");
            Check(from.Count(f => f < 0) == 3, "the hand feeds exactly three arms");
            bool closest = true;
            for (int i = 0; i < into.Count; i++)
                if (from[i] >= 0)
                {
                    Check(from[i] < i, "each links to one already in the circuit");
                    float d = into[i].Pos.DistanceTo(into[from[i]].Pos);
                    for (int j = 0; j < i; j++)
                        if (into[i].Pos.DistanceTo(into[j].Pos) < d - 0.01f)
                            closest = false;
                }
            Check(closest, "each joins the closest enemy already in it once the hand's arms are used");
            Game.CwCircuit(new V2(0f, 0f), new V2(0f, 0f), 330f, new List<Enemy> { es[0], es[6] }, e => e.Pos, e => true, 3, into, from);
            Check(into.Count == 2 && from.All(f => f < 0), "two enemies on opposite sides, fewer than the arms: each takes its own from the hand");
        }

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Web", Role.Healer, 0, 0, Kit.Elementalist, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Elementalist, 0, 3);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.ConduitWeb && host.Lobby[cid].Ult == 3 && client.Lobby[1].Ult == 3, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.ConduitWeb && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.ConduitWeb,
            "in the run both players' ultimate is Conduit Web, on both games");

        // --- The web on the host's game ---------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(1, true);
        V2 mePos = new V2(700f, 450f), allyPos = new V2(100f, 100f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.Pos = at; mine.Add(e.Id); return e;
        };
        var pinned = new Dictionary<Enemy, V2>();  // enemies held where the test put them (they'd walk otherwise)
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var kv in pinned) kv.Key.Pos = kv.Value;
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(300f, 0f) });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); pinned.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.CwT = 0f; me.CwUp = false;
            me.CwWired = me.CwPulses = me.CwMostWired = 0; me.CwHealed = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            allyPos = new V2(100f, 100f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float dmg = me.Stats.DmgMult;

        // Casting: the field bursts out.
        reset();
        cast();
        Check(me.CwT == Game.CwTime && me.CwLen == Game.CwTime && me.CwR == Game.CwRadius && me.UltT == me.CwT && Game.CwOn(me), "casting: a 330 px field for 4 s");
        Check(Game.CwReach(me) < 1f, "it starts at your body");
        step(Game.CwGrow + 0.05f);
        Check(Math.Abs(Game.CwReach(me) - Game.CwRadius) < 0.5f, "and bursts out to its full reach in 0.35 s");
        step(0.1f);
        var cp = client.Game.Players[1];
        Check(cp.CwT > 0f && cp.CwR == Game.CwRadius && cp.CwLen == Game.CwTime, "other screens see the web");

        // Enemies in the field: wired, hurt on the wire, held stunned and shocked each pulse.
        reset();
        var near = make(mePos + new V2(120f, 0f), EnemyType.Grunt);
        var edge = make(mePos + new V2(0f, 300f), EnemyType.Grunt);
        var outside = make(mePos + new V2(-(Game.CwRadius + 80f), 0f), EnemyType.Grunt);
        pinned[near] = near.Pos; pinned[edge] = edge.Pos; pinned[outside] = outside.Pos;
        cast();
        step(Game.CwGrow + dt);
        Check(me.CwWired == 2 && near.StunT > 0f && edge.StunT > 0f && outside.StunT <= 0f, "the two in the field are wired and stunned, the one outside isn't");
        float first = near.MaxHp - near.Hp;
        Check(first >= Game.CwFirstDmg * dmg - 0.5f, "the wire hits for 12");
        Check(outside.Hp == outside.MaxHp, "the one outside isn't hurt");
        float hpA = near.Hp;
        step(1f);
        float oneSec = hpA - near.Hp;
        Console.WriteLine("  conduit web: wire {0:0}, then {1:0} in the next second (shock x{2:0.00})", first, oneSec, dmg);
        Check(me.CwPulses >= 2 && oneSec >= 2f * Game.CwDmg * dmg - 0.5f, "shocked every half second (8 and growing)");
        step(Game.CwRamp - 1f);
        float hpB = near.Hp;
        step(Game.CwPulse);
        Check(hpB - near.Hp >= 2f * Game.CwDmg * dmg - 0.5f, "wired 3 s, a pulse hits twice as hard (16)");
        Check(near.StunT > 0f, "still held stunned while wired");

        // One walking in is wired too.
        reset();
        var walker = make(mePos + new V2(Game.CwRadius + 60f, 0f), EnemyType.Grunt);
        pinned[walker] = walker.Pos;
        cast();
        step(0.6f);
        Check(walker.Hp == walker.MaxHp && me.CwWired == 0, "an enemy just past the field isn't wired");
        pinned[walker] = mePos + new V2(Game.CwRadius - 40f, 0f);
        step(dt * 2f);
        Check(me.CwWired == 1 && walker.StunT > 0f && walker.Hp < walker.MaxHp, "once it walks in, it's wired");

        // Bosses: shocked, never stunned.
        reset();
        var boss = make(mePos + new V2(150f, 0f), FirstEnemy(d => d.IsBoss));
        pinned[boss] = boss.Pos;
        cast();
        step(0.6f);
        Check(boss.Def.IsBoss && boss.Hp < boss.MaxHp && boss.StunT <= 0f, "a boss (" + boss.Type + ") is shocked but not stunned");

        // Teammates in the field heal each pulse; out of it they don't.
        reset();
        allyPos = mePos + new V2(-150f, 60f);
        ally.Hp = ally.MaxHp * 0.3f;
        cast();
        step(1.05f);
        Console.WriteLine("  conduit web: a teammate in the field healed {0:0.0} in a second", me.CwHealed);
        Check(me.CwHealed >= 2f * Game.CwHeal - 0.01f && me.CwHealed <= 2f * Game.CwHeal + 0.01f, "a teammate in the field heals 5 each pulse");
        allyPos = mePos + new V2(0f, -(Game.CwRadius + 100f));
        float h0 = me.CwHealed;
        step(1f);
        Check(me.CwHealed == h0, "not outside it");

        // It ends on time: those still wired stay stunned 1 s more.
        reset();
        var held = make(mePos + new V2(100f, 50f), EnemyType.Grunt);
        pinned[held] = held.Pos;
        cast();
        step(Game.CwTime + 0.05f);
        Check(me.CwT == 0f && !me.CwUp && me.UltT == 0f && !Game.CwOn(me), "it ends after 4 s");
        Check(held.StunT > Game.CwStunAfter - 0.1f, "the enemy still wired stays stunned 1 s more");
        step(0.2f);
        Check(client.Game.Players[1].CwT == 0f, "other screens see it end");

        // Going down ends it.
        reset();
        cast();
        step(1f);
        me.Downed = true;
        step(dt);
        Check(me.CwT == 0f && !me.CwUp, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.ConduitWeb] = 1e6f;
        var wide = make(mePos + new V2(Game.CwRadius + 50f, 0f), EnemyType.Grunt);
        pinned[wide] = wide.Pos;
        cast();
        Check(me.CwR == Game.CwRadiusMastered && me.CwT == Game.CwTimeMastered, "mastered: 420 px for 6 s");
        step(0.6f);
        Check(wide.StunT > 0f && wide.Hp < wide.MaxHp, "mastered: an enemy 380 px out is wired");
        me.MasteryXp.Remove(AbilityId.ConduitWeb);
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot casts it in a crowd");

        host.Leave(""); client.Leave("");
    }
}
