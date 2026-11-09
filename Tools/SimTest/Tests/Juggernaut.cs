using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Arena;

// The Juggernaut Suit (Juggernaut.cs) and the lobby's Ultimate row (LobbyEntry.Ult): the pick reaching the host and the other players,
// clamped to what the role and class has; the armour soaking before health; a flak shard back at the shooter per bullet the armour
// takes; the slow and the taunt; ending on time, on armour 0, on going down and between waves; the mastered numbers; the bot's rule;
// and the pick surviving into the match and a saved run.
static partial class Program
{
    static void JuggernautTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var enemyShot = typeof(Game).GetMethod("EnemyShot", flags);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var tickJugg = typeof(Game).GetMethod("TickJugg", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: the Ultimate row's pick ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.JuggernautSuit);
        Check(def.Name == "Juggernaut Suit" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Tank && def.Kit == Kit.Soldier,
            "the Juggernaut Suit is the Tank Soldier's ultimate on key 1, 75 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Soldier) == AbilityId.Fortress && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 0) == AbilityId.Fortress
            && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 1) == AbilityId.JuggernautSuit && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 2) == AbilityId.Fortress,
            "Fortress stays the default; key 1 is the Juggernaut Suit; an empty key falls back to the default");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Soldier, 1) == 1 && Upgrades.ClampUlt(Role.Tank, Kit.Soldier, 2) == 0 && Upgrades.ClampUlt(Role.Healer, Kit.Soldier, 1) == 0
            && Upgrades.ClampUlt(Role.Wildcard, Kit.Soldier, 1) == 0 && Upgrades.Ultimate(Role.Wildcard, Kit.Soldier, 1) == Upgrades.Ultimate(Role.Wildcard, Kit.Soldier),
            "picks are clamped to the keys the role and class has (a Wildcard's is always 0)");
        int alts = 0;
        for (int r = 0; r < 3; r++)
            for (int k = 0; k < Defs.KitCount; k++)
                for (int u = 1; u < Upgrades.UltKeys; u++)
                    if (Upgrades.AltUltimate((Role)r, (Kit)k, u) != AbilityId.None)
                        alts++;
        Check(alts == 3 && Upgrades.AltUltimate(Role.Tank, Kit.Elementalist, 1) == AbilityId.LightningRod && Upgrades.AltUltimate(Role.Tank, Kit.Hemomancer, 1) == AbilityId.Bloodbath, "only the Tank Soldier, Tank Elementalist (Lightning Rod) and Tank Hemomancer (Bloodbath) have a key 1; every other role and class keeps its three locked slots");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Soldier);  // (AutoStart: StartMatch below needs no Ready)
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(1);
        client.RequestUlt(1);
        settle();
        int cid = client.MyId;
        Check(host.Lobby[1].Ult == 1 && host.Lobby[cid].Ult == 1 && client.Lobby[1].Ult == 1 && client.Lobby[cid].Ult == 1
            && host.Lobby[1].UltId == AbilityId.JuggernautSuit, "the pick reaches the host and the other players");
        client.RequestUlt(3);
        settle();
        Check(host.Lobby[cid].Ult == 0 && client.Lobby[cid].Ult == 0, "the host clamps a pick of a locked key back to the default");
        client.RequestUlt(1);
        client.RequestKit(Kit.Elementalist);
        settle();
        Check(host.Lobby[cid].Ult == 0, "changing class goes back to the default ultimate");
        client.RequestKit(Kit.Soldier);
        settle();
        client.RequestUlt(1);
        settle();
        Check(host.Lobby[cid].Ult == 1 && !host.Lobby[cid].Ready, "picking takes back your Ready");
        host.RequestUlt(0);
        settle();
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Fortress && g.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.JuggernautSuit
            && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.JuggernautSuit && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.Fortress,
            "in the match each player's F is the one they picked, on every game");

        // --- The suit on the host's game ------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        me.Slots[Upgrades.UltSlot].Id = AbilityId.JuggernautSuit;
        me.Slots[Upgrades.UltSlot].MaxCharges = Math.Max(1, me.Slots[Upgrades.UltSlot].MaxCharges);
        V2 mePos = new V2(500f, 450f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => !mine.Contains(x.Shooter)).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = new V2(200f, 200f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.UltT = 0f; me.JuggT = 0f; me.JuggArmor = 0f; me.JuggShards = 0; me.TauntT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });

        reset();
        float speed0 = me.Speed;
        var a = make(new V2(800f, 300f), EnemyType.Grunt);
        var b = make(new V2(800f, 600f), EnemyType.Brute);
        a.TargetId = b.TargetId = cid;
        a.RetargetT = b.RetargetT = 5f;
        cast();
        Check(me.JuggT == Game.JuggTime && me.JuggArmor == Game.JuggArmor && me.JuggMax == Game.JuggArmor && me.UltT == Game.JuggTime, "casting puts on 300 armour for 10 s");
        Check(Math.Abs(me.Speed / speed0 - Game.JuggSlow) < 1e-4f, "you move 25% slower");
        Check(a.TargetId == 1 && b.TargetId == 1 && (int)pickTarget.Invoke(g, null) == 1, "every enemy turns on you (PickTarget, which bosses use too)");
        Check(me.TauntT <= 0f, "it doesn't use Roar's damage cut");
        step(0.3f);
        Check(client.Game.Players[1].JuggT > 0f && Math.Abs(client.Game.Players[1].JuggArmor - me.JuggArmor) < 0.01f && client.Game.Players[1].JuggMax == Game.JuggArmor,
            "other screens see the suit's time and armour");

        // The armour soaks before health.
        float hp0 = me.Hp, arm0 = me.JuggArmor;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        Console.WriteLine("  jugg: 40 damage -> armour {0:0.0} -> {1:0.0}, health {2:0.0} -> {3:0.0}", arm0, me.JuggArmor, hp0, me.Hp);
        Check(me.JuggArmor < arm0 && me.Hp == hp0, "the armour takes damage before your health");

        // A shard back at the shooter for each bullet the armour takes, and no hit grace for it.
        me.InvulnT = 0f;
        var shooter = make(new V2(800f, 450f), EnemyType.Grunt);
        var bystander = make(new V2(760f, 380f), EnemyType.Grunt);
        float sh0 = shooter.Hp, by0 = bystander.Hp;
        hp0 = me.Hp; arm0 = me.JuggArmor;
        bool tracer = false, tracerSeen = false;
        for (int i = 0; i < 3; i++)
        {
            enemyShot.Invoke(g, new object[] { shooter, MathX.Pi, 420f, Defs.SmallShot, 0f });
            step(0.12f);
        }
        for (int i = 0; i < 70; i++)
        {
            step(dt);
            tracer |= g.Effects.Any(e => e.Kind == FxKind.JuggShard && (int)e.Radius == 1 && e.Pos2.DistanceTo(shooter.Pos) < 1f);
            tracerSeen |= client.Game.Effects.Any(e => e.Kind == FxKind.JuggShard);
        }
        float dealt = sh0 - shooter.Hp;
        Console.WriteLine("  jugg: 3 shots -> {0} shards, shooter took {1:0.0} (expect {2:0.0}), armour {3:0.0} -> {4:0.0}, health {5:0.0} -> {6:0.0}",
            me.JuggShards, dealt, 3 * Game.JuggShard * me.Stats.DmgMult, arm0, me.JuggArmor, hp0, me.Hp);
        Check(me.JuggShards == 3 && Math.Abs(dealt - 3 * Game.JuggShard * me.Stats.DmgMult) < 0.5f && bystander.Hp == by0, "each bullet the armour takes throws a 12-damage shard straight back at whoever fired it");
        Check(me.Hp == hp0 && me.JuggArmor < arm0 && me.InvulnT <= 0.01f, "the shots wear the armour, not your health, and leave no hit grace");
        Check(tracer && tracerSeen, "each shard sends a tracer from the plates to the shooter, seen on other screens too");

        // Armour gone: it ends, and what's left of the hit reaches your health.
        me.JuggArmor = 3f;
        hp0 = me.Hp;
        me.InvulnT = 0f;
        enemyShot.Invoke(g, new object[] { shooter, MathX.Pi, 420f, Defs.SmallShot, 0f });
        step(1.2f);
        Check(me.JuggT == 0f && me.JuggArmor == 0f && me.UltT == 0f && me.Hp < hp0, "it ends when the armour is gone, the rest of the hit reaching your health");
        Check(Math.Abs(me.Speed - speed0) < 1e-4f, "the slow is gone");

        // Time.
        reset();
        cast();
        step(Game.JuggTime - 0.5f);
        Check(me.JuggT > 0f, "it's still on just before 10 s");
        step(0.7f);
        Check(me.JuggT == 0f && me.JuggArmor == 0f && me.UltT == 0f, "it ends when the 10 s run out");
        Check(me.Slots[Upgrades.UltSlot].Charges == 0 && me.Slots[Upgrades.UltSlot].Timer > 0f, "the cooldown is running");

        // Going down.
        reset();
        cast();
        me.JuggArmor = 1f;
        me.Hp = 1f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        step(0.1f);
        Check(me.Downed && me.JuggT == 0f, "it ends if you go down");

        // Between waves (as Last Stand).
        reset();
        cast();
        g.State = MatchState.Intermission;
        tickJugg.Invoke(g, new object[] { dt });
        Check(me.JuggT == 0f, "it ends when the wave is over");
        g.State = MatchState.Fighting;

        // Mastered: 450 armour and 18 a shard.
        reset();
        me.MasteryXp[AbilityId.JuggernautSuit] = 1e6f;
        cast();
        var sh2 = make(new V2(800f, 450f), EnemyType.Grunt);
        float s20 = sh2.Hp;
        enemyShot.Invoke(g, new object[] { sh2, MathX.Pi, 420f, Defs.SmallShot, 0f });
        step(1.2f);
        Check(me.JuggMax == Game.JuggArmorMastered && Math.Abs(s20 - sh2.Hp - Game.JuggShardMastered * me.Stats.DmgMult) < 0.5f, "mastered: 450 armour and 18 a shard");
        me.MasteryXp.Remove(AbilityId.JuggernautSuit);
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot puts it on with several enemies near");
        host.Leave(""); client.Leave("");

        // --- A saved run keeps the pick -------------------------------------------------------------------------------
        var save = new RunSave { Id = "jugg", Wave = 2 };
        save.Seats.Add(new SeatSave { Name = "Tank", Role = Role.Tank, Kit = Kit.Soldier, Ult = 1, Level = 3 });
        var back = RunSave.Parse(save.ToText(), "jugg");
        var old = RunSave.Parse(save.ToText().Replace("Tank Soldier 0 0 0 1", "Tank Soldier 0 0 0"), "old");
        Check(back?.Seats[0].Ult == 1 && old?.Seats[0].Ult == 0, "a save keeps the pick (an older save without it reads as the default)");
        string folder = Path.Combine(Path.GetTempPath(), "arena-jugg-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        string oldFolder = SaveStore.Folder;
        SaveStore.Folder = folder;
        try
        {
            port = nextPort++;
            var h2 = new Session { AutoStart = 9 };
            h2.HostResume(back, port, "Tank");
            h2.RequestUlt(0);  // (a continued run's loadout is the seat's: this is ignored)
            Check(h2.Lobby[1].Ult == 1, "continuing a saved run puts the pick back in the lobby");
            h2.StartMatch();
            for (int i = 0; i < 30; i++) Step(h2, dt);
            Check(h2.Game.Running && h2.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.JuggernautSuit && h2.Game.Players[1].UltPick == 1,
                "and into the continued match");
            h2.Leave("");
        }
        finally
        {
            SaveStore.Folder = oldFolder;
            try { Directory.Delete(folder, true); } catch { }
        }
    }
}
