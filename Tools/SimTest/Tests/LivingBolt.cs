using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Living Bolt (LivingBolt.cs): key 1 of the DPS Elementalist's Ultimate row reaching the run and a save; the channel (rooted,
// no dash); a click dashes to the cursor (260 px at most), shocking enemies on the path (40, a stun; heavies only hurt) and
// leaving a residue lane (12 a second, once a tick however many lanes); a held Fire doesn't repeat, the lockout; the weapon
// put away; a client's dash reaching the host; the discharge on time; going down; mastered; the bot's rule.
static partial class Program
{
    static void LivingBoltTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 1 of the DPS Elementalist's Ultimate row -----------------------------------------------------
        var def = Upgrades.Ability(AbilityId.LivingBolt);
        Check(def.Name == "Living Bolt" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Dps && def.Kit == Kit.Elementalist,
            "Living Bolt is the DPS Elementalist's ultimate on key 1, 80 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Elementalist, 1) == AbilityId.LivingBolt && Upgrades.Ultimate(Role.Dps, Kit.Elementalist) == AbilityId.BulletRain
            && Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 1) == AbilityId.LightningRod,
            "key 1 is Living Bolt; Bullet Rain stays the default; the tank's key 1 is still Lightning Rod");
        Check(Upgrades.ClampUlt(Role.Dps, Kit.Elementalist, 1) == 1 && Upgrades.ClampUlt(Role.Dps, Kit.Elementalist, 3) == 0 && Upgrades.ClampUlt(Role.Healer, Kit.Knight, 1) == 0,  // (the Healer Knight has one ultimate)
            "key 1 is the DPS's; other keys and roles fall back to the default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Volt", Role.Dps, 0, 0, Kit.Elementalist, 0, 1);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0, 0, Kit.Elementalist, 0, 1);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1);
        Check(host.Lobby[1].Ult == 1 && host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.LivingBolt, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.LivingBolt && cg.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.LivingBolt,
            "in the run both players' ultimate is Living Bolt, on both games");
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Volt", Role = Role.Dps, Kit = Kit.Elementalist, Ult = 1 });
        save.Seats.Add(new SeatSave { Name = "Tank", Role = Role.Tank, Kit = Kit.Elementalist, Ult = 1 });
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats[0].Ult == 1 && back.Seats[1].Ult == 1, "a saved run keeps key 1");

        // --- On the host's game -------------------------------------------------------------------------------------------
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
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)Math.Round(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                foreach (var e in g.Enemies.Values) if (mine.Contains(e.Id)) e.FrozenT = Math.Max(e.FrozenT, 0.5f);  // (they stay put)
                ally.Pos = new V2(150f, 150f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            foreach (var id in mine) g.Enemies.Remove(id);
            mine.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            foreach (var id in g.Zones.Keys.ToList()) g.Zones.Remove(id);
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.BoltT = 0f; me.BoltUp = false; me.BoltLockT = 0f;
            me.BoltDashes = me.BoltShocks = me.BoltResidueHits = me.BoltDischarged = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = home; masteredField.SetValue(g, false);
            fire = false; aim = home + new V2(300f, 0f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, me.Pos, false });
        float dmg = me.Stats.DmgMult;
        Action click = () => { fire = true; step(dt); fire = false; step(dt); };

        // The channel: rooted, and a click does nothing yet.
        reset();
        cast();
        Check(Math.Abs(me.BoltT - (Game.BoltChannel + Game.BoltTime)) < 1e-3f && me.UltT == me.BoltT && Game.BoltOn(me) && !Game.BoltCharged(me),
            "casting: a 0.5 s channel, then 10 s");
        Check(Game.BoltSpeed(me) == 0f, "you can't move while it channels");
        click();
        Check(me.BoltDashes == 0 && me.Pos.DistanceTo(home) < 1f, "a click in the channel doesn't dash");

        // A click dashes to the cursor, shocking what's on the path and leaving residue.
        step(Game.BoltChannel);
        Check(Game.BoltCharged(me) && Game.BoltSpeed(me) == 1f, "past the channel you're electrified and move freely");
        var onPath = make(home + new V2(150f, 12f), EnemyType.Grunt);
        var heavyType = HeavyFoe(moving: true);
        var heavy = make(home + new V2(220f, -20f), heavyType);
        var offPath = make(home + new V2(150f, 120f), EnemyType.Grunt);
        foreach (var e in new[] { onPath, heavy, offPath }) e.FrozenT = 0f;
        aim = home + new V2(1000f, 0f);
        click();
        float moved = me.Pos.DistanceTo(home);
        Console.WriteLine("  livingbolt: a click 1000 px away moved {0:0} px; the grunt on the path took {1:0}, the heavy {2:0}, the one off it {3:0}",
            moved, onPath.MaxHp - onPath.Hp, heavy.MaxHp - heavy.Hp, offPath.MaxHp - offPath.Hp);
        Check(Math.Abs(moved - Game.BoltRange) < 2f && me.BoltDashes == 1, "a click blinks you to the cursor, 260 px at most");
        Check(onPath.MaxHp - onPath.Hp >= Game.BoltDamage * dmg - 0.5f && onPath.StunT > 0f, "an enemy on the path is shocked (40) and stunned");
        Check(heavy.MaxHp - heavy.Hp >= Game.BoltDamage * dmg - 0.5f && heavy.StunT <= 0f, "a heavy (" + heavyType + ") is hurt but not stunned");
        Check(offPath.Hp == offPath.MaxHp, "one off the path is left alone");
        Check(g.Effects.Any(e => e.Kind == FxKind.LivingBolt) && g.Zones.Values.Count(z => z.Kind == ZoneKind.StaticResidue) == 1, "with the bolt's look and one residue lane");
        Check(me.InvulnT > 0f || me.BoltDashes == 1, "you can't be hurt for the blink");

        // Holding Fire doesn't repeat; the lockout stops a click straight after.
        fire = true;
        aim = me.Pos + new V2(0f, 200f);
        step(0.5f);
        fire = false;
        step(dt);
        Check(me.BoltDashes == 2, "holding Fire dashes once, not again and again (a press in the lockout goes when it ends)");
        step(Game.BoltLock);
        aim = me.Pos + new V2(-200f, 0f);
        click();
        Check(me.BoltDashes == 3, "the next click dashes again");
        aim = me.Pos + new V2(0f, -200f);
        click();
        Check(me.BoltDashes == 3, "a click inside the 0.12 s lockout waits");
        step(Game.BoltLock);
        Check(me.BoltDashes == 4, "and goes as soon as it ends");

        // The weapon is put away.
        int shots = g.Bullets.Values.Count(b => b.Owner == 1);
        fire = true; step(0.5f); fire = false; step(dt);
        Check(g.Bullets.Values.Count(b => b.Owner == 1) == shots, "nothing fires while electrified: Fire is the dash");

        // The residue: 12 a second, once a tick however many lanes cross.
        reset();
        cast();
        step(Game.BoltChannel + dt);
        var sitter = make(home + new V2(100f, 60f), EnemyType.Grunt);  // off the dash paths, on the lanes laid below
        sitter.Pos = home + new V2(400f, 400f);
        aim = home + new V2(200f, 0f); click(); step(Game.BoltLock);
        aim = home; click(); step(Game.BoltLock);
        aim = home + new V2(200f, 0f); click(); step(Game.BoltLock);
        int lanes = g.Zones.Values.Count(z => z.Kind == ZoneKind.StaticResidue);
        sitter.Pos = home + new V2(100f, 6f);
        float hp0 = sitter.Hp;
        step(1f);
        float perSec = hp0 - sitter.Hp;
        Console.WriteLine("  livingbolt: {0} lanes over one spot: {1:0.0} damage a second (x{2:0.00})", lanes, perSec, dmg);
        Check(lanes == 3 && Math.Abs(perSec - Game.ResidueDps * dmg) <= Game.ResidueDps * Game.ResidueTick * dmg + 0.5f, "a residue lane hurts 12 a second, once however many cross");
        step(Game.ResidueTime);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.StaticResidue), "the residue is gone after 3 s");

        // The discharge when it ends on time.
        reset();
        cast();
        step(Game.BoltChannel + dt);
        var nearBy = make(home + new V2(0f, 100f), EnemyType.Grunt);
        var farOff = make(home + new V2(0f, -260f), EnemyType.Grunt);
        me.BoltT = 0.02f;
        step(0.05f);
        Check(me.BoltT == 0f && me.UltT == 0f && !me.BoltUp && !Game.BoltOn(me), "it ends on time");
        Check(nearBy.MaxHp - nearBy.Hp >= Game.DischargeDamage * dmg - 0.5f && farOff.Hp == farOff.MaxHp && me.BoltDischarged == 1,
            "and discharges: 60 to enemies within 130 px");

        // Going down ends it, with no discharge.
        reset();
        cast();
        step(Game.BoltChannel + dt);
        nearBy = make(home + new V2(0f, 100f), EnemyType.Grunt);
        me.Downed = true;
        step(dt);
        Check(me.BoltT == 0f && !me.BoltUp && nearBy.Hp == nearBy.MaxHp, "going down ends it at once, with no discharge");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered.
        reset();
        me.MasteryXp[AbilityId.LivingBolt] = 1e6f;
        cast();
        Check(Math.Abs(me.BoltT - (Game.BoltChannel + Game.BoltTimeMastered)) < 1e-3f, "mastered: 13 s");
        step(Game.BoltChannel + dt);
        click();
        var lane = g.Zones.Values.FirstOrDefault(z => z.Kind == ZoneKind.StaticResidue);
        Check(lane != null && lane.Duration == Game.ResidueTimeMastered, "mastered: the residue stays 5 s");
        me.MasteryXp.Remove(AbilityId.LivingBolt);

        // A client's dash: its own game moves it and the host shocks along the path.
        reset();
        g.DevSetGod(cid, true);
        ally.Slots[Upgrades.UltSlot].Charges = 1; ally.Slots[Upgrades.UltSlot].Timer = 0f;
        hostAbility.Invoke(g, new object[] { cid, Upgrades.UltSlot, ally.Pos, false });
        Check(ally.BoltUp, "the client's ultimate is cast on the host");
        V2 cHome = new V2(150f, 150f);
        var cTarget = make(cHome + new V2(120f, 0f), EnemyType.Grunt);
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
                g.FixedStep(dt, new PlayerInput { AimAt = aim });
                cg.FixedStep(dt, new PlayerInput { AimAt = cHome + new V2(240f, 0f), Fire = cFire });
                Nap(1);
            }
        };
        cme.Pos = cHome; ally.Pos = cHome;
        both(Game.BoltChannel + 0.4f);
        Check(Game.BoltCharged(cme), "the client's game sees its own charge");
        cme.Pos = cHome; ally.Pos = cHome;
        cFire = true; both(dt); cFire = false;
        both(0.3f);
        Console.WriteLine("  livingbolt: the client moved {0:0} px on its own game; the host counted {1} dash(es), the grunt took {2:0}",
            cme.Pos.DistanceTo(cHome), ally.BoltDashes, cTarget.MaxHp - cTarget.Hp);
        Check(cme.Pos.DistanceTo(cHome) > 200f && ally.BoltDashes == 1 && cTarget.Hp < cTarget.MaxHp, "a client's click moves it on its own game and the host shocks the path");

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, HaveTarget = true, TargetD = 200f }) && !def.Bot(new BotSense { Me = me, HaveTarget = false }), "a bot casts it with a target in reach");

        host.Leave(""); client.Leave("");
    }
}
