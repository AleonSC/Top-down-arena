using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Rock Gloves (RockGloves.cs): key 3 of the DPS Elementalist's Ultimate row; the gather (slowed, no punches); held Fire jabs at the
// cursor, the fists taking turns (26 along the fist's path, 130 px; nothing behind or off to the side); the 0.09 s rate; every 8th
// punch the Haymaker (80 in 90 px, a stun; heavies only hurt); the weapon put away; the fields reaching a client; the shards on
// time; going down (no shards); mastered; Granite Knuckles; the bot's rule.
static partial class Program
{
    static void RockGlovesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby -----------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.RockGloves);
        Check(def.Name == "Rock Gloves" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Dps && def.Kit == Kit.Elementalist,
            "Rock Gloves is the DPS Elementalist's ultimate on key 3, 80 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 3) == AbilityId.RockGloves && Upgrades.Ultimate(Role.Dps, Kit.Elementalist) == AbilityId.BulletRain
            && Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 1) == AbilityId.LivingBolt && Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 2) == AbilityId.FlashFire,
            "key 3 is Rock Gloves; Bullet Rain, Living Bolt and Flash Fire keep keys 0-2");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Rock", Role.Dps, 0, 0, Kit.Elementalist, 0, 3);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Soldier, 0, 0);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        Run(s, dt, 2, () => client.Lobby[1].Ult == 3);
        Check(host.Lobby[1].UltId == AbilityId.RockGloves && client.Lobby[1].Ult == 3, "the pick reaches the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.RockGloves, "in the run the ultimate is Rock Gloves");
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.DevSetGod(1, true);
        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        bool fire = false;
        V2 aim = home + new V2(300f, 0f);
        var pinned = new Dictionary<Enemy, V2>();
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var kv in pinned) kv.Key.Pos = kv.Value;  // (they stay put)
                ally.Pos = new V2(150f, 150f);
                me.Pos = home;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); pinned.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.GlvT = 0f; me.GlvUp = false; me.GlvCd = 0f;
            me.GlvJabs = me.GlvHays = me.GlvHits = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = home; masteredField.SetValue(g, false);
            fire = false; aim = home + new V2(300f, 0f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        Func<V2, EnemyType, Enemy> pin = (at, type) => { var e = make(at, type); e.FrozenT = 0f; pinned[e] = at; return e; };  // (not frozen: earth would shatter it)
        float dmg = me.Stats.DmgMult;

        // The gather: slowed, and Fire does nothing yet.
        reset();
        cast();
        Check(Math.Abs(me.GlvT - (Game.GlvGather + Game.GlvTime)) < 1e-3f && me.UltT == me.GlvT && Game.GlvOn(me) && !Game.GlvReady(me),
            "casting: a 0.6 s gather, then 10 s");
        Check(Game.GlvSpeed(me) == Game.GlvGatherMove, "you walk at half speed while the fists gather");
        fire = true; step(0.3f); fire = false;
        Check(me.GlvJabs == 0, "Fire in the gather doesn't punch");
        step(Game.GlvGather);
        Check(Game.GlvReady(me) && Game.GlvSpeed(me) == 1f, "past the gather the fists are out and you move freely");

        // A jab: what's along the fist's path, not behind or off to the side.
        var front = pin(home + new V2(100f, 8f), EnemyType.Grunt);
        var behind = pin(home + new V2(-90f, 0f), EnemyType.Grunt);
        var aside = pin(home + new V2(60f, 110f), EnemyType.Grunt);
        var far = pin(home + new V2(200f, 0f), EnemyType.Grunt);
        fire = true; step(dt); fire = false; step(dt);
        Console.WriteLine("  rockgloves: one jab: the grunt in front took {0:0}, behind {1:0}, aside {2:0}, 200 px out {3:0}",
            front.MaxHp - front.Hp, behind.MaxHp - behind.Hp, aside.MaxHp - aside.Hp, far.MaxHp - far.Hp);
        Check(me.GlvJabs == 1 && Math.Abs(front.MaxHp - front.Hp - Game.GlvJabDamage * dmg) < 1f,
            "a jab hits what's in front (26)");
        Check(behind.Hp == behind.MaxHp && aside.Hp == aside.MaxHp && far.Hp == far.MaxHp, "nothing behind, to the side or past 130 px");
        Check(front.BrittleT > 0f, "earth: the hit leaves it brittle");

        // Held Fire: a jab every 0.09 s, the fists taking turns, the 8th a Haymaker.
        reset();
        cast();
        step(Game.GlvGather + dt);
        var target = pin(home + new V2(110f, 0f), EnemyType.Grunt);
        var heavyType = HeavyFoe(moving: true);
        var heavy = pin(home + new V2(150f, 60f), heavyType);
        var outside = pin(home + new V2(120f + Game.GlvHayRadius + 60f, 0f), EnemyType.Grunt);
        fire = true;
        float t0 = 0f;
        while (me.GlvJabs < 7 && t0 < 2f) { step(dt); t0 += dt; }
        Console.WriteLine("  rockgloves: 7 jabs in {0:0.00} s (one every {1:0.000} s)", t0, t0 / 6f);
        Check(t0 <= 6f * Game.GlvJabEvery + 3f * dt + 0.02f, "held Fire jabs about every 0.09 s");
        Check(Game.GlvPunchNo(me) == 7 && me.GlvHays == 0, "the punch number is replicated through GlvPunch");
        float heavyBefore = heavy.Hp;
        while (me.GlvHays < 1 && t0 < 3f) { step(dt); t0 += dt; }
        Check(me.GlvHays == 1 && Game.GlvIsHay(Game.GlvPunchNo(me)), "the 8th punch is the Haymaker");
        Check(heavy.Hp < heavyBefore && heavy.StunT <= 0f && target.StunT > 0f, "the Haymaker hurts all near (" + heavyType + " not stunned, grunts stunned)");
        Check(outside.Hp == outside.MaxHp, "and nothing past its 90 px");
        int jabsAt = me.GlvJabs;
        step(Game.GlvHayRecover - 2f * dt);
        Check(me.GlvJabs == jabsAt, "the next jab waits 0.32 s after a Haymaker");
        int shots = g.Bullets.Values.Count(b => b.Owner == 1);
        step(0.3f);
        fire = false;
        Check(me.GlvJabs > jabsAt && g.Bullets.Values.Count(b => b.Owner == 1) == shots, "then the jabs go on, and your weapon fires nothing: it's put away");

        // The fields reach the client.
        for (int i = 0; i < 4; i++) { host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); cg.FixedStep(dt, new PlayerInput()); Nap(1); }
        var seen = cg.Players[1];
        Check(seen.GlvT > 0f && Math.Abs(seen.GlvAge - me.GlvAge) < 0.2f && Game.GlvPunchNo(seen) > 0, "the client sees the fists, their age and the punch");

        // On time: the fists burst into shards.
        reset();
        cast();
        step(Game.GlvGather + dt);
        var ring = new List<Enemy>();
        for (int i = 0; i < 12; i++) ring.Add(pin(home + V2.FromAngle(MathX.Tau * i / 12f) * 100f, EnemyType.Grunt));
        me.GlvT = 0.02f;
        step(0.05f);
        Check(me.GlvT == 0f && me.UltT == 0f && !me.GlvUp && !Game.GlvOn(me), "it ends on time");
        step(Game.ColRollTime);
        int struck = ring.Count(e => e.MaxHp - e.Hp >= Game.GlvShardDamage * dmg - 0.5f);
        Console.WriteLine("  rockgloves: the shards hit {0} of 12 grunts in a ring 100 px out", struck);
        Check(struck >= 6, "and the fists burst into 8 shards that hit what they run over (20)");
        var near = ring[0];

        // Going down ends it with no shards.
        reset();
        cast();
        step(Game.GlvGather + dt);
        ring.Clear();
        for (int i = 0; i < 12; i++) ring.Add(pin(home + V2.FromAngle(MathX.Tau * i / 12f) * 100f, EnemyType.Grunt));
        me.Downed = true;
        step(dt);
        me.Downed = false; me.Hp = me.MaxHp;
        step(Game.ColRollTime);
        Check(me.GlvT == 0f && !me.GlvUp && ring.All(e => e.Hp == e.MaxHp), "going down ends it at once, with no shards");

        // Mastered, and Granite Knuckles.
        reset();
        me.MasteryXp[AbilityId.RockGloves] = 1e6f;
        cast();
        Check(Math.Abs(me.GlvT - (Game.GlvGather + Game.GlvTimeMastered)) < 1e-3f && Game.GlvHayReach(me) > Game.GlvHayRadius * 1.2f, "mastered: 13 s and a 25% wider Haymaker");
        me.MasteryXp.Remove(AbilityId.RockGloves);
        var card = Upgrades.All.First(u => u.Name == "Granite Knuckles");
        var st = new Stats();
        card.Apply(st);
        Check(card.RequiresAbility == AbilityId.RockGloves && Math.Abs(st.GlvDmgMult - 1.2f) < 1e-4f && Math.Abs(st.GlvReachMult - 1.15f) < 1e-4f,
            "Granite Knuckles: punches 20% harder, 15% farther");

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = 3, HaveTarget = true, TargetD = 150f }) && !def.Bot(new BotSense { Me = me, Crowd = 1, HaveTarget = true, TargetD = 150f }),
            "a bot casts it with 3 enemies near and a target in reach");

        host.Leave(""); client.Leave("");
    }
}
