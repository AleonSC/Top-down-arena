using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Blood Golem (BloodGolem.cs): key 2 of the Tank Hemomancer's Ultimate row; the gather (stacks torn off enemies in reach, the rest paid
// from health, the taunt, the slow walk); the golem (12 coats of armour that soak hits and shed, 75% speed, other screens seeing it);
// Golem Fist (cone, a Slam every third, stacks, the weapon put away); Hurl Clot (a coat thrown, the splash and the pool); kills growing a
// coat back; the collapse's burst and heals; ending when the armour is gone or on going down; mastered; the bot's rule.
static partial class Program
{
    static void BloodGolemTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BloodGolem);
        Check(def.Name == "Blood Golem" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Tank && def.Kit == Kit.Hemomancer,
            "Blood Golem is the Tank Hemomancer's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Hemomancer) == AbilityId.VeinTithe && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath
            && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 2) == AbilityId.BloodGolem && Upgrades.ClampUlt(Role.Tank, Kit.Hemomancer, 2) == 2 && Upgrades.ClampUlt(Role.Tank, Kit.Hemomancer, 3) == 0,
            "Vein Tithe stays the default, Bloodbath key 1, Blood Golem key 2; key 3 falls back");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Golem", Role.Tank, 0, 0, Kit.Hemomancer, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Hemomancer, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.BloodGolem && client.Lobby[1].Ult == 2, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BloodGolem && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.BloodGolem,
            "in the run both players' F is Blood Golem, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(500f, 450f), aimAt = new V2(900f, 450f), allyPos = new V2(200f, 200f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; mine.Add(e.Id); return e;
        };
        bool fire = false, alt = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt, Fire = fire, WeaponAction = alt });
            }
        };
        Action clearFoes = () => { foreach (var id in mine.ToList()) g.Enemies.Remove(id); mine.Clear(); };
        Action reset = () =>
        {
            clearFoes(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.UltT = 0f; me.GolT = 0f; me.GolUp = false; me.GolArmor = 0f; me.GolCoats = 0f;
            me.GolPunches = me.GolSlams = me.GolKills = me.GolRipped = 0; me.TauntT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            fire = alt = false; allyPos = new V2(200f, 200f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });

        // --- The gather ----------------------------------------------------------------------------------------------
        reset();
        float speed0 = me.Speed;
        var near = make(mePos + new V2(120f, 0f), EnemyType.Grunt);
        near.BlastStacks = 3; near.BleedStacks = 1;
        var far = make(mePos + new V2(-420f, 0f), EnemyType.Grunt);
        far.BlastStacks = 4; far.TargetId = cid; far.RetargetT = 5f;
        float hp0 = me.Hp;
        cast();
        Check(Math.Abs(me.GolT - (Game.GolGather + Game.GolTime)) < 1e-3f && me.UltT == me.GolT && Game.GolOn(me) && !Game.GolStanding(me), "casting: a 2 s gather, then 10 s");
        Check(far.TargetId == 1, "every enemy turns on you");
        Check(Math.Abs(me.Speed / speed0 - Game.GolMoveGather) < 1e-4f, "you walk at half speed while it gathers");
        step(1.05f);
        Check(near.BlastStacks == 0 && near.BleedStacks == 0 && me.GolRipped == 4 && Math.Abs(me.GolCoats - 4f) < 1e-3f, "the 4 stacks within 300 px are torn off: 4 coats");
        Check(far.BlastStacks == 4, "an enemy beyond 300 px keeps its stacks");
        Check(near.MaxHp - near.Hp > 0f && g.Effects.Any(e => e.Kind == FxKind.Drain), "each torn stack hurts it, and its blood flows to you");
        step(1.05f);
        float paid = (hp0 - me.Hp) / me.MaxHp;
        Console.WriteLine("  golem: {0} coats from stacks, {1:0.0}% of max health paid for the rest", me.GolRipped, paid * 100f);
        Check(Game.GolStanding(me) && Math.Abs(me.GolCoats - 12f) < 1e-3f, "it forms with 12 coats");
        Check(Math.Abs(paid - 8 * Game.GolCoatCost) < 0.02f, "the other 8 coats cost 2.5% of max health each");
        Check(Math.Abs(me.GolArmor - 12f * Game.GolCoatArmor * me.MaxHp) < 0.5f, "12 coats are 96% of your max health in armour");
        Check(client.Game.Players[1].GolT > 0f && Math.Abs(client.Game.Players[1].GolCoats - 12f) < 0.01f, "other screens see the golem and its coats");
        Check(Math.Abs(me.Speed / speed0 - Game.GolMove) < 1e-4f, "the golem walks at 75%");

        // --- The armour soaks and sheds ---------------------------------------------------------------------------------
        me.InvulnT = 0f; me.Hp = me.MaxHp; float armor0 = me.GolArmor;
        damage.Invoke(g, new object[] { me, 50f, "test" });
        Console.WriteLine("  golem: 50 damage -> armour {0:0.0} off, health {1:0.0} off", armor0 - me.GolArmor, me.MaxHp - me.Hp);
        Check(me.Hp == me.MaxHp && armor0 - me.GolArmor >= 50f - 0.5f, "a hit comes off the coats, not your health");
        g.Effects.Clear();
        me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, me.GolPer * 2f, "test" });
        step(dt);
        int shed = g.Effects.Count(e => e.Kind == FxKind.GolemFx && Math.Abs(e.Radius - Game.GolFxShed) < 0.1f);
        Check(shed >= 2 && me.GolCoats < 10.5f, "coats shot off fall away as chunks");
        me.GolArmor = 12f * me.GolPer; me.GolCoats = 12f; me.GolShown = 12; me.InvulnT = 0f;

        // --- Golem Fist: left, right, Slam ------------------------------------------------------------------------------
        clearFoes();
        var front = make(mePos + new V2(80f, 0f), EnemyType.Grunt);
        var behind = make(mePos + new V2(-80f, 0f), EnemyType.Grunt);
        var side = make(mePos + new V2(30f, 100f), EnemyType.Grunt);
        // (they stay frozen in place: a grunt would back off to shoot)
        g.Bullets.Clear();
        float dmg = me.Stats.DmgMult;
        fire = true;
        step(dt);
        fire = false;
        float firstHit = front.MaxHp - front.Hp;
        Console.WriteLine("  golem: a fist hit for {0:0.0}", firstHit);
        Check(me.GolPunches == 1 && firstHit > Game.GolFistDamage * dmg * 0.9f && behind.Hp == behind.MaxHp, "a fist hits in front (45), not behind");
        Check(front.BlastStacks == 1 && front.BlastOwner == 1, "the hit adds a blood stack");
        fire = true;
        step(Game.GolPunchEvery + 0.05f);
        step(Game.GolPunchEvery + 0.05f);
        fire = false;
        Check(me.GolSlams == 1 && me.GolPunches == 2, "the third swing is a Slam");
        Console.WriteLine("  golem: the Slam hit the side for {0:0.0}, stun {1:0.00}", side.MaxHp - side.Hp, side.StunT);
        Check(side.MaxHp - side.Hp > Game.GolSlamDamage * dmg * 0.9f && side.StunT > 0.2f, "the Slam hits all round (70) and stuns");
        Check(!g.Bullets.Values.Any(b => b.Owner == 1), "the weapon is put away");
        Check(client.Game.Players[1].GolPunch >= 1f, "other screens see the swings");

        // --- Hurl Clot --------------------------------------------------------------------------------------------------
        clearFoes();
        var target = make(mePos + new V2(300f, 0f), EnemyType.Grunt);
        aimAt = target.Pos;
        float coats0 = me.GolCoats;
        alt = true;
        step(dt);
        alt = false;
        Check(Math.Abs(coats0 - me.GolCoats - 1f) < 0.01f, "Hurl Clot tears off a coat");
        step(Game.GolClotFlight + 0.1f);
        Check(target.MaxHp - target.Hp > Game.GolClotDamage * dmg * 0.9f, "the clot splashes where it lands (60)");
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.BloodPool && z.Pos.DistanceTo(target.Pos) < 5f), "it leaves a Gore Mortar pool");
        alt = true; step(dt); alt = false;
        Check(Math.Abs(coats0 - me.GolCoats - 1f) < 0.01f, "Hurl Clot has a cooldown");
        aimAt = new V2(900f, 450f);

        // --- A kill grows a coat back -----------------------------------------------------------------------------------
        clearFoes();
        var weak = make(mePos + new V2(70f, 0f), EnemyType.Grunt);
        weak.Hp = 5f; weak.FrozenT = 0f;
        me.GolArmor = 6f * me.GolPer; me.GolCoats = 6f; me.GolShown = 6;
        step(0.5f);
        fire = true; step(dt); fire = false;
        step(dt);
        Check(!g.Enemies.ContainsKey(weak.Id) && me.GolKills == 1 && Math.Abs(me.GolCoats - 7f) < 0.01f, "a kill grows a coat back");

        // --- The collapse -----------------------------------------------------------------------------------------------
        clearFoes();
        var ring = new List<Enemy>();
        for (int i = 0; i < 6; i++)
            ring.Add(make(mePos + V2.FromAngle(MathX.Tau * i / 6f) * 150f, EnemyType.Grunt));
        var outside = make(mePos + new V2(260f, 0f), EnemyType.Grunt);
        me.GolArmor = 5f * me.GolPer; me.GolCoats = 5f; me.GolShown = 5;
        me.Hp = me.MaxHp * 0.5f; float hpBefore = me.Hp;
        allyPos = mePos + new V2(0f, 120f);
        g.DevSetGod(cid, false); ally.Hp = ally.MaxHp * 0.5f; float allyBefore = ally.Hp;
        me.GolT = 0.02f;
        step(dt * 2f);
        Check(!Game.GolOn(me) && me.UltT == 0f && me.GolBurst == 5, "it ends on time with 5 coats left");
        Check(ring.All(e => e.MaxHp - e.Hp > 5f * Game.GolBurstDamage * dmg * 0.9f) && outside.Hp == outside.MaxHp, "each coat left bursts for 20 within 180 px");
        Check(me.Hp - hpBefore >= 5f * Game.GolBurstHeal * me.MaxHp - 0.5f, "and gives you back 2% of max health a coat");
        Check(ally.Hp - allyBefore >= 5f * Game.GolBurstAllyHeal * ally.MaxHp - 0.5f, "allies in the burst heal 1% a coat");
        Check(g.Effects.Any(e => e.Kind == FxKind.GolemFx && Math.Abs(e.Radius - Game.GolFxCollapse) < 0.1f), "the collapse is sent");
        Check(Math.Abs(me.Speed / speed0 - 1f) < 1e-4f, "your speed is back");
        g.DevSetGod(cid, true);

        // --- Armour gone, going down ------------------------------------------------------------------------------------
        reset();
        cast();
        step(Game.GolGather + 0.1f);
        me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, me.GolArmor + 1f, "test" });
        step(dt * 2f);
        Check(!Game.GolOn(me) && me.GolBurst == 0, "it ends when the armour is gone");
        reset();
        cast();
        step(0.5f);
        me.Downed = true;
        step(dt);
        Check(me.GolT == 0f, "going down ends it at once");
        me.Downed = false;

        // --- Mastered -----------------------------------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.BloodGolem] = 1e6f;
        cast();
        Check(Math.Abs(me.GolT - (Game.GolGather + Game.GolTimeMastered)) < 1e-3f && me.GolMastered, "mastered: 13 s");
        me.MasteryXp.Remove(AbilityId.BloodGolem);
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot becomes the golem in a crowd");

        host.Leave(""); client.Leave("");
    }
}
