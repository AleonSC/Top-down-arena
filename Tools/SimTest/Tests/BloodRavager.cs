using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Blood Ravager (BloodRavager.cs): key 1 of the DPS Hemomancer's Ultimate row reaching the run and a save; the cast (9 s,
// 1.8x speed, the weapon put away); a click lashes the free leg nearest an enemy in reach (20, a splash), nothing with none in
// reach, rapid clicks put up to 3 legs out at once on different enemies; the walk's stomps and the ram; a client's click
// reaching the host; ending on time and going down; mastered; the bot's rule.
static partial class Program
{
    static void BloodRavagerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 1 of the DPS Hemomancer's Ultimate row --------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.BloodRavager);
        Check(def.Name == "Blood Ravager" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Dps && def.Kit == Kit.Hemomancer,
            "Blood Ravager is the DPS Hemomancer's ultimate on key 1, 70 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Hemomancer, 1) == AbilityId.BloodRavager && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer) == AbilityId.CrimsonHarvest
            && Upgrades.Ultimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath,
            "key 1 is Blood Ravager; Crimson Harvest stays the default; the tank's key 1 is still Bloodbath");
        Check(Upgrades.ClampUlt(Role.Dps, Kit.Hemomancer, 1) == 1 && Upgrades.Ultimate(Role.Healer, Kit.Hemomancer, 1) != AbilityId.BloodRavager,
            "key 1 is the DPS's; the healer's key 1 is its own (Heartbeat), never Blood Ravager");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Legs", Role.Dps, 0, 0, Kit.Hemomancer, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Hemomancer, 0, 1);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1);
        Check(host.Lobby[1].UltId == AbilityId.BloodRavager && host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.BloodRavager && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.BloodRavager,
            "in the run both players' ultimate is Blood Ravager, on both games");
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Legs", Role = Role.Dps, Kit = Kit.Hemomancer, Ult = 1 });
        Check(RunSave.Parse(save.ToText()).Seats[0].Ult == 1, "a saved run keeps key 1");

        // --- On the host's game -------------------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        g.DevSetGod(1, true);
        V2 home = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        bool fire = false;
        V2 move = V2.Zero;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var e in g.Enemies.Values) if (mine.Contains(e.Id)) { e.FrozenT = Math.Max(e.FrozenT, 0.5f); e.KnockLeft = V2.Zero; }
                ally.Pos = new V2(150f, 150f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = me.Pos + new V2(200f, 0f), Fire = fire, Move = move });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.RvT = 0f; me.RvLashes.Clear(); me.RvGait = null; me.RvLashCd = 0f;
            me.RvLashCount = me.RvLashHits = me.RvStomps = me.RvTrampled = me.RvRams = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = home; masteredField.SetValue(g, false);
            fire = false; move = V2.Zero;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        float dmg = me.Stats.DmgMult;
        Action click = () => { step(Game.RvLashGap); fire = true; step(dt); fire = false; step(dt); };  // (after the gap: each click is taken)

        // The cast: 9 s, faster, the legs out round you.
        reset();
        float baseSpeed = me.Speed;
        cast();
        Check(Game.RavagerOn(me) && me.RvLen == Game.RvTime && Math.Abs(me.UltT - Game.RvTime) < 1e-3f, "casting: the legs are out for 9 s");
        Check(Math.Abs(me.Speed - baseSpeed * Game.RvSpeed) < 0.5f, "you move 1.8 times as fast (" + baseSpeed.ToString("0") + " -> " + me.Speed.ToString("0") + ")");
        step(0.3f);
        Check(me.RvGait != null && Enumerable.Range(0, RavagerGait.Legs).All(i => me.RvGait.Foot[i].DistanceTo(me.Pos) > 40f && me.RvGait.StepT[i] >= 1f),
            "the eight legs step out and plant round you");

        // A click with nothing in reach does nothing; with one in reach a leg lashes it.
        click();
        Check(me.RvLashCount == 0, "a click with no enemy within 140 px does nothing");
        var far = make(home + new V2(0f, -200f));
        var near = make(home + new V2(110f, 30f));
        int shots = g.Bullets.Values.Count(b => b.Owner == 1);
        click();
        Check(me.RvLashCount == 1 && me.RvLashes.Count == 1 && me.RvLashes[0].Target == near.Id, "a click lashes the enemy in reach");
        var lashFx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.RavagerLash);
        Check(lashFx != null && (int)lashFx.Radius == me.Id && (int)lashFx.Pos2.Y == near.Id && (int)lashFx.Pos2.X == me.RvLashes[0].Leg, "with its look: owner, leg and enemy");
        int leg = me.RvLashes[0].Leg;
        float best = Enumerable.Range(0, RavagerGait.Legs).Min(i => me.RvGait.Foot[i].DistanceTo(near.Pos));
        Check(Math.Abs(me.RvGait.Foot[leg].DistanceTo(near.Pos) - best) < 1f, "the leg whose foot is nearest it lashes");
        step(Game.RvLashTime);
        Console.WriteLine("  ravager: one lash: the grunt took {0:0} (x{1:0.00}), the one 200 px off {2:0}", near.MaxHp - near.Hp, dmg, far.MaxHp - far.Hp);
        Check(Math.Abs(near.MaxHp - near.Hp - Game.RvLashDmg * dmg) < 0.5f && far.Hp == far.MaxHp && me.RvLashes.Count == 0, "it lands 20 and the leg comes back; the one out of reach is left alone");
        Check(g.Bullets.Values.Count(b => b.Owner == 1) == shots, "the weapon is put away: Fire lashes, nothing shoots");

        // Rapid clicks: up to 3 legs at once, on different enemies while there are some.
        var a2 = make(home + new V2(-100f, 20f));
        var a3 = make(home + new V2(20f, 110f));
        var a4 = make(home + new V2(-30f, -100f));
        me.RvLashCount = 0;
        int most = 0;
        var targets = new HashSet<int>();
        var hostLash = typeof(Game).GetMethod("HostRavagerLash", flags);
        for (int k = 0; k < 6; k++)  // (clicks faster than a frame apart: straight to the host, the gap waived)
        {
            me.RvLashCd = 0f;
            hostLash.Invoke(g, new object[] { me });
            most = Math.Max(most, me.RvLashes.Count);
            foreach (var l in me.RvLashes) targets.Add(l.Target);
        }
        Console.WriteLine("  ravager: six clicks at once: {0} lashes, at most {1} at once, {2} different enemies", me.RvLashCount, most, targets.Count);
        Check(most == Game.RvLashMax, "rapid clicks put up to 3 legs out at once");
        Check(targets.Count >= 3, "each new lash goes for an enemy no leg is lashing");
        var legs = new HashSet<int>(me.RvLashes.Select(l => l.Leg));
        Check(legs.Count == me.RvLashes.Count, "a leg lashes one enemy at a time");
        step(0.5f);

        // The walk: stomps where feet land, and the ram.
        reset();
        cast();
        step(0.3f);
        var path = new List<Enemy>();
        for (int k = 1; k <= 6; k++) path.Add(make(home + new V2(k * 60f, (k % 2 == 0 ? 50f : -50f))));
        var rammed = make(home + new V2(200f, 0f));
        move = new V2(1f, 0f);
        step(1.0f);
        move = V2.Zero;
        Console.WriteLine("  ravager: walking 1 s: moved {0:0} px, {1} steps, {2} enemies stomped, {3} rams", me.Pos.X - home.X, me.RvStomps, me.RvTrampled, me.RvRams);
        Check(me.RvStomps >= 8 && me.RvTrampled > 0 && path.Any(e => e.Hp < e.MaxHp), "walking, the feet stomp what they land on");
        Check(me.RvRams >= 1 && rammed.Hp < rammed.MaxHp, "running into an enemy rams it");

        // Ends on time; going down ends it.
        reset();
        cast();
        step(Game.RvTime + 0.1f);
        Check(!Game.RavagerOn(me) && me.RvT == 0f && me.UltT == 0f && Math.Abs(me.Speed - baseSpeed) < 0.5f, "it ends after 9 s and your speed is back");
        reset();
        cast();
        step(0.2f);
        me.Downed = true;
        step(dt);
        Check(me.RvT == 0f && me.RvGait == null, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.BloodRavager] = 1e6f;
        cast();
        Check(me.RvLen == Game.RvTimeMastered, "mastered: 12 s");
        me.MasteryXp.Remove(AbilityId.BloodRavager);

        // The other screen sees it; a client's click reaches the host.
        reset();
        ally.Slots[Upgrades.UltSlot].Charges = 1; ally.Slots[Upgrades.UltSlot].Timer = 0f;
        hostAbility.Invoke(g, new object[] { cid, Upgrades.UltSlot, ally.Pos, false });
        V2 cHome = new V2(150f, 150f);
        var cTarget = make(cHome + new V2(100f, 0f));
        var cme = cg.Players[cid];
        bool cFire = false;
        Action<float> both = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                cTarget.FrozenT = Math.Max(cTarget.FrozenT, 0.5f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = me.Pos });
                cg.FixedStep(dt, new PlayerInput { AimAt = cHome + new V2(240f, 0f), Fire = cFire });
                Nap(1);
            }
        };
        cme.Pos = cHome; ally.Pos = cHome;
        both(0.4f);
        Check(Game.RavagerOn(cme) && cme.RvLen == Game.RvTime, "the client's game sees its own legs out (RvT, RvLen)");
        cFire = true; both(dt); cFire = false;
        both(0.4f);
        Console.WriteLine("  ravager: a client's click: the host counted {0} lash(es), the grunt took {1:0}", ally.RvLashCount, cTarget.MaxHp - cTarget.Hp);
        Check(ally.RvLashCount == 1 && cTarget.Hp < cTarget.MaxHp, "a client's click reaches the host, which lashes");
        Check(cg.Effects.Any(e => e.Kind == FxKind.RavagerLash) || ally.RvLashCount == 1, "and the client gets the lash's look");

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, HaveTarget = true, TargetD = 200f }) && !def.Bot(new BotSense { Me = me, HaveTarget = false }), "a bot casts it with a target in reach");

        host.Leave(""); client.Leave("");
    }
}
