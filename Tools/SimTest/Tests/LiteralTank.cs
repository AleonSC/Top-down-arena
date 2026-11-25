using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// A Literal Tank (LiteralTank.cs): the Tank Soldier's key 2 on the lobby's Ultimate row, the pick reaching the run; the tank's
// armour before health, the slow, the main gun in place of your weapon (a shell every 0.9 s: 75 to what it hits, 45 round it, a
// burst at the cursor when it hits nobody), small enemies run over and heavies shoved, abilities locked; the other screen sees it;
// it ends on time (the wreck blowing up), on the armour running out, on going down and between waves.
static partial class Program
{
    static void LiteralTankTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var tickTanks = typeof(Game).GetMethod("TickTanks", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.LiteralTank);
        Check(def.Name == "A Literal Tank" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Tank && def.Kit == Kit.Soldier && def.Capstone != null,
            "A Literal Tank is the Tank Soldier's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Soldier) == AbilityId.Fortress && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 1) == AbilityId.JuggernautSuit
            && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 2) == AbilityId.LiteralTank && Upgrades.ClampUlt(Role.Tank, Kit.Soldier, 2) == 2 && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 2) != AbilityId.LiteralTank,
            "Fortress stays the default and the Juggernaut Suit key 1; key 2 is the tank, and only for the Tank Soldier");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Driver", Role.Tank, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.LiteralTank, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        int cid = client.MyId;
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.LiteralTank && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.LiteralTank,
            "in the match the host's Ultimate is the tank, on every game");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(800f, 450f);
        V2 aim = mePos + new V2(400f, 0f);
        bool fire = false;
        var mine = new HashSet<int>();
        Func<EnemyType, V2, float, Enemy> make = (type, at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FrozenT = 999f; e.FireT = 999f; e.Pos = at; mine.Add(e.Id); return e;
        };
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(b => b.Kind != Defs.TankShell).Select(b => b.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = new V2(200f, 800f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.UltT = 0f; me.TankT = 0f; me.TankArmor = 0f; me.TankShells = me.TankCrushes = me.TankWrecks = 0;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            fire = false; aim = mePos + new V2(400f, 0f);
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        EnemyType heavy = Enum.GetValues(typeof(EnemyType)).Cast<EnemyType>().First(t => (int)t < Defs.Enemies.Length && Defs.Get(t) != null && Defs.Get(t).Heavy && !Defs.Get(t).IsBoss);

        // In, the armour, the slow.
        reset();
        float speed = me.Speed;
        cast();
        Check(me.TankT == Game.TankTime && me.TankArmor == Game.TankArmor && me.TankMax == Game.TankArmor && me.UltT == Game.TankTime && Game.TankOn(me),
            "the tank lands on you: 15 s, 400 armour");
        Check(Math.Abs(me.Speed - speed * Game.TankSlow) < 0.01f, "you drive at 75% speed");
        step(0.1f);
        Check(client.Game.Players[1].TankT > 0f && client.Game.Players[1].TankMax == Game.TankArmor, "the other screen sees the tank");
        float hp0 = me.Hp;
        me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, 100f, "test" });
        Check(me.Hp == hp0 && Math.Abs(me.TankArmor - (Game.TankArmor - 100f)) < 0.01f, "the armour takes damage before your health");

        // Abilities are locked inside.
        float slot0 = me.Slots[0].Timer;
        int charges0 = me.Slots[0].Charges;
        hostAbility.Invoke(g, new object[] { 1, 0, mePos, false });
        Check(me.Slots[0].Charges == charges0 && me.Slots[0].Timer == slot0, "no abilities while you're inside");

        // The main gun: a direct hit, and the blast round it.
        var hitE = make(EnemyType.Grunt, mePos + new V2(250f, 0f), 1e6f);
        var nearE = make(EnemyType.Grunt, mePos + new V2(250f, 60f), 1e6f);
        step(Game.TankDrop);
        fire = true;
        step(0.05f);
        fire = false;
        step(0.5f);
        float dm = me.Stats.DmgMult;
        Console.WriteLine("  tank: shells {0}, hit lost {1:0.0}, near lost {2:0.0}, dmg mult {3:0.00}", me.TankShells, 1e6f - hitE.Hp, 1e6f - nearE.Hp, dm);
        Check(me.TankShells == 1 && Math.Abs(1e6f - hitE.Hp - Game.TankHit * dm) < 1f, "a shell does 75 to what it hits");
        Check(Math.Abs(1e6f - nearE.Hp - Game.TankBlast * dm) < 1f, "and 45 to what's round it");
        fire = true;
        step(1.8f + dt);
        fire = false;
        Check(me.TankShells == 3, "one shell every 0.9 s while you hold Fire (" + me.TankShells + ")");
        g.Enemies.Remove(hitE.Id); g.Enemies.Remove(nearE.Id); mine.Clear();
        step(1f);

        // A miss bursts at the cursor.
        var atCursor = make(EnemyType.Grunt, mePos + new V2(0f, -300f) + new V2(0f, -30f), 1e6f);
        aim = mePos + new V2(0f, -300f);
        g.Players[1].Aim = -MathX.Pi / 2f;
        int before = me.TankShells;
        fire = true;
        step(0.05f);
        fire = false;
        step(0.6f);
        Check(me.TankShells == before + 1 && Math.Abs(1e6f - atCursor.Hp - Game.TankBlast * dm) < 1f, "a shell that hits nobody bursts at the cursor for 45 (" + (1e6f - atCursor.Hp).ToString("0.0") + ")");
        g.Enemies.Remove(atCursor.Id); mine.Clear();

        // Running enemies over.
        var small = make(EnemyType.Grunt, mePos + new V2(20f, 0f), 500f);
        var big = make(heavy, mePos + new V2(-25f, 0f), 1e6f);
        V2 bigAt = big.Pos;
        step(0.2f);
        Check(!g.Enemies.ContainsKey(small.Id) || small.Hp <= 0f, "a small enemy you drive into is crushed");
        Check(big.Hp < 1e6f && Math.Abs(1e6f - big.Hp - Game.TankCrush * dm) < 1f && big.Pos.DistanceTo(mePos) > bigAt.DistanceTo(mePos), "a heavy takes 25 and is shoved");

        // It ends on time, and the wreck blows up.
        g.Enemies.Remove(big.Id); mine.Clear();
        var bystander = make(EnemyType.Grunt, mePos + new V2(100f, 0f), 1e6f);
        step(me.TankT + 0.1f);
        Check(me.TankT == 0f && !Game.TankOn(me) && me.UltT == 0f && me.TankWrecks == 1, "you hop out when the time runs out");
        Check(Math.Abs(1e6f - bystander.Hp - Game.TankWreck * dm) < 1f, "and the empty tank blows up (90 round it)");
        Run(s, dt, 1, () => client.Game.Players[1].TankT == 0f);
        Check(client.Game.Players[1].TankT == 0f, "the other screen sees it go");

        // Mastered: more armour, a wider blast.
        reset();
        me.MasteryXp[AbilityId.LiteralTank] = 1e6f;
        cast();
        Check(me.TankArmor == Game.TankArmorMastered && me.TankMastered, "mastered: 550 armour");
        me.MasteryXp.Remove(AbilityId.LiteralTank);

        // The armour running out, going down, the wave ending.
        reset();
        cast();
        me.InvulnT = 0f;
        damage.Invoke(g, new object[] { me, Game.TankArmor + 5f, "test" });
        tickTanks.Invoke(g, new object[] { dt });
        Check(!Game.TankOn(me) && me.TankT == 0f, "it's over when the armour runs out");
        reset();
        cast();
        me.InvulnT = 0f; me.TankArmor = 1f; me.Hp = 1f;
        damage.Invoke(g, new object[] { me, 500f, "test" });
        tickTanks.Invoke(g, new object[] { dt });
        Check(me.Downed && me.TankT == 0f, "and if you go down");
        reset();
        cast();
        g.State = MatchState.Intermission;
        tickTanks.Invoke(g, new object[] { dt });
        Check(me.TankT == 0f && me.UltT == 0f, "and when the wave is over");
        g.State = MatchState.Fighting;
        host.Leave(""); client.Leave("");
    }
}
