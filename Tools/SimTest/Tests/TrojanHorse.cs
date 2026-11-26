using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Trojan Horse (TrojanHorse.cs): the Tank Knight's key 1 on the lobby's Ultimate row, the pick reaching the run; the horse rolling
// out along your aim at 90 px/s, shoving enemies aside and parking at the arena's edge; climbing in (your own Interact, and a
// client's Msg.HorseBoard), riding unhurt and untargeted with abilities locked, arrows out of it on Fire; enemy shots stopping
// on it and wearing it; everyone in: enemies go for the horse; it breaking (riders out, untouchable a moment), Sally Forth on
// time (riders out with +25% damage, enemies near it shoved), the cooldown waiting for it, mastered health, ending between waves.
static partial class Program
{
    static void TrojanHorseTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var setRide = typeof(Game).GetMethod("SetRide", flags);
        var wear = typeof(Game).GetMethod("WearHorse", flags);
        var pick = typeof(Game).GetMethod("PickTarget", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby ---------------------------------------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.TrojanHorse);
        Check(def.Name == "Trojan Horse" && def.Cooldown == 75f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Tank && def.Kit == Kit.Knight && def.Capstone != null,
            "Trojan Horse is the Tank Knight's ultimate on key 1, 75 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Knight) == AbilityId.LastStand && Upgrades.Ultimate(Role.Tank, Kit.Knight, 1) == AbilityId.TrojanHorse
            && Upgrades.ClampUlt(Role.Tank, Kit.Knight, 1) == 1 && Upgrades.Ultimate(Role.Dps, Kit.Knight, 1) != AbilityId.TrojanHorse,
            "Last Stand stays the default; key 1 is the horse, and only for the Tank Knight");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9 }, port, false, "Rider", Role.Tank, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(1);
        settle();
        Check(host.Lobby[1].Ult == 1 && client.Lobby[1].Ult == 1 && host.Lobby[1].UltId == AbilityId.TrojanHorse, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        int cid = client.MyId;
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.TrojanHorse && client.Game.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.TrojanHorse,
            "in the match the host's Ultimate is the horse, on every game");

        var me = g.Players[1];
        var ally = g.Players[cid];
        V2 mePos = new V2(700f, 450f), allyPos = new V2(200f, 800f);
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
                foreach (var e in g.Enemies.Values) e.FireT = 999f;
                if (!Game.Riding(me)) me.Pos = mePos;
                if (!Game.Riding(ally)) ally.Pos = allyPos;
                me.Aim = (aim - me.Pos).Angle;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        Func<Zone> horse = () => g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.TrojanHorse && q.T > 0f);
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); g.Zones.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            foreach (var p in new[] { me, ally }) { p.Hp = p.MaxHp; p.Downed = false; p.HorseId = 0; p.SallyT = 0f; p.InvulnT = 0f; p.HorseArrows = 0; }
            me.UltT = 0f; me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; masteredField.SetValue(g, false);
            mePos = new V2(700f, 450f); allyPos = new V2(200f, 800f);
            me.Pos = mePos; ally.Pos = allyPos;
            fire = false; aim = mePos + new V2(400f, 0f); me.Aim = 0f;
            g.State = MatchState.Fighting;
        };
        Action cast = () => { me.Aim = (aim - me.Pos).Angle; hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, false }); };

        // --- The roll ------------------------------------------------------------------------------------------------------
        reset();
        var inPath = make(EnemyType.Grunt, mePos + new V2(60f, 6f), 1e6f);
        cast();
        var z0 = horse();
        Check(z0 != null && Math.Abs(z0.SoakMax - Game.HorseHpFor(2, false)) < 0.01f && z0.SoakMax == 1400f && me.UltT == Game.HorseTime,
            "the horse rolls out where you stand: 1,400 health for two players");
        float cd0 = me.Slots[Upgrades.UltSlot].Timer;
        step(1f);
        Check(Math.Abs(z0.Pos.X - (mePos.X + Game.HorseSpeed)) < 3f && Math.Abs(z0.Pos.Y - mePos.Y) < 0.5f && !z0.Quiet, "it rolls along your aim at 90 px/s (" + z0.Pos.X.ToString("0") + ")");
        Check(Math.Abs(inPath.Pos.Y - mePos.Y) > Game.HorseHalfW, "an enemy in its path is shoved aside (" + (inPath.Pos.Y - mePos.Y).ToString("0") + " px)");
        Check(me.Slots[Upgrades.UltSlot].Timer == cd0, "the cooldown waits while it's out");
        Run(s, dt, 2, () => client.Game.Zones.Values.Any(q => q.Kind == ZoneKind.TrojanHorse));
        Check(client.Game.Zones.Values.Any(q => q.Kind == ZoneKind.TrojanHorse && q.Marched > 50f && q.GuardFrac > 0.99f), "the other screen sees it roll");
        step(12f);
        Check(z0.Quiet && z0.Pos.X > Game.Width - Game.HorseNose - 20f && z0.Pos.X + Game.HorseNose <= Game.Width, "it parks at the arena's edge (" + z0.Pos.X.ToString("0") + ")");
        float parkedX = z0.Pos.X;
        step(1f);
        Check(z0.Pos.X == parkedX, "and stays there");

        // --- Climbing in -----------------------------------------------------------------------------------------------------
        reset();
        cast();
        var z = horse();
        step(0.5f);
        mePos = z.Pos + new V2(0f, 60f);
        step(dt);
        g.HorseInteract();
        Check(Game.Riding(me) && me.HorseId == z.Id && me.HorseBoards > 0, "Interact by the horse climbs you in");
        step(0.5f);
        Check(me.Pos.DistanceTo(z.Pos) < 1f, "inside, you go where it goes");
        float hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 60f, "test" });
        Check(me.Hp == hp0, "nothing hurts you inside");
        float slot0 = me.Slots[0].Timer;
        int charges0 = me.Slots[0].Charges;
        hostAbility.Invoke(g, new object[] { 1, 0, mePos, false });
        Check(me.Slots[0].Charges == charges0 && me.Slots[0].Timer == slot0, "no abilities while you're inside");

        // Arrows out of it.
        var target = make(EnemyType.Grunt, z.Pos + new V2(0f, -250f), 1e6f);
        aim = target.Pos;
        fire = true;
        step(1f);
        fire = false;
        step(0.5f);
        float dm = me.Stats.DmgMult;
        Console.WriteLine("  horse: arrows {0}, target lost {1:0.0}, dmg mult {2:0.00}", me.HorseArrows, 1e6f - target.Hp, dm);
        Check(me.HorseArrows == 3, "Fire shoots an arrow every 0.35 s (" + me.HorseArrows + ")");
        Check(Math.Abs(1e6f - target.Hp - 3f * Game.HorseArrowDmg * dm) < 1f, "16 each");
        g.Enemies.Remove(target.Id); mine.Clear();

        // A client climbs in over the wire.
        allyPos = z.Pos + new V2(0f, -60f);
        step(0.3f);
        client.Game.Me.Pos = allyPos;
        client.Game.HorseInteract();
        Run(s, dt, 1, () => Game.Riding(ally));
        Check(Game.Riding(ally) && ally.HorseId == z.Id, "a client's Interact climbs them in (Msg.HorseBoard)");
        Run(s, dt, 1, () => client.Game.Players[cid].HorseId == z.Id);
        Check(client.Game.Players[cid].HorseId == z.Id && client.Game.HorseOf(client.Game.Players[cid]) != null, "their screen knows they're in");

        // Everyone in: the enemies go for the horse.
        Check((int)pick.Invoke(g, null) == 0, "with everyone inside there's nobody to pick");
        z.Quiet = true;  // (parked here, so a walker can reach it)
        var hunter = make(EnemyType.Brute, z.Pos + new V2(-350f, 0f), 1e6f);
        hunter.FrozenT = 0f;
        float d0 = hunter.Pos.DistanceTo(z.Pos);
        step(1.5f);
        Check(g.HorseHunted(z) && hunter.Pos.DistanceTo(z.Pos) < d0 - 30f, "everyone in: they go for the horse (" + d0.ToString("0") + " -> " + hunter.Pos.DistanceTo(z.Pos).ToString("0") + ")");
        float bodyHp = z.BlockHp;
        step(5f);
        Check(hunter.Pos.DistanceTo(Game.HorseSpine(z, hunter.Pos)) >= Game.HorseHalfW + hunter.Radius - 1f && z.BlockHp < bodyHp, "they can't walk into it, and their bites wear it");
        setRide.Invoke(g, new object[] { ally, false });
        allyPos = ally.Pos;
        Check(!Game.Riding(ally) && ally.Pos.DistanceTo(z.Pos) > 40f && !g.HorseHunted(z), "Interact again drops you out beside it, and the hunt is off");
        Check((int)pick.Invoke(g, null) == cid, "and the enemies pick whoever is outside");
        g.Enemies.Remove(hunter.Id); mine.Clear();

        // Enemy shots stop on it and wear it.
        float hpB = z.BlockHp;
        var shot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.PierceShot, z.Pos + new V2(-200f, 0f), new V2(400f, 0f), 0f, 3f, 20f, 0, false, 0f });
        step(0.8f);
        Check(!g.Bullets.ContainsKey(shot.Id) && z.BlockHp < hpB, "an enemy shot stops on it, red aimed ones too, and wears it");

        // --- It breaks ---------------------------------------------------------------------------------------------------------
        wear.Invoke(g, new object[] { z, z.BlockHp + 10f });
        Check(!Game.Riding(me) && me.HorseId == 0 && me.InvulnT >= Game.HorseBreakInvuln - 0.01f && me.SallyT == 0f && horse() == null && me.UltT == 0f,
            "at 0 health it breaks: you're thrown out, untouchable a moment");
        Check(g.Effects.Any(e => e.Kind == FxKind.TrojanEnd && e.Radius > 0.5f), "the planks fly");
        float cdAfter = me.Slots[Upgrades.UltSlot].Timer;
        step(0.5f);
        Check(me.Slots[Upgrades.UltSlot].Timer < cdAfter, "and the cooldown starts");

        // --- Sally Forth -------------------------------------------------------------------------------------------------------
        reset();
        cast();
        z = horse();
        mePos = z.Pos;
        step(dt);
        g.HorseInteract();
        var near = make(EnemyType.Grunt, z.Pos + new V2(0f, 70f), 1e6f);
        step(Game.HorseTime - 0.5f);
        near.Pos = z.Pos + new V2(0f, 70f);
        V2 zAt = z.Pos;
        step(1f);
        Check(!Game.Riding(me) && me.SallyT > Game.HorseSallyTime - 1f && horse() == null, "after 20 s it springs open: Sally Forth");
        Check(near.Pos.DistanceTo(zAt) > 70f + 60f, "enemies by it are shoved away (" + near.Pos.DistanceTo(zAt).ToString("0") + ")");
        Check(g.Effects.Any(e => e.Kind == FxKind.TrojanEnd && e.Radius < 0.5f), "its sides swing open");
        float before = near.Hp;
        hurt.Invoke(g, new object[] { near, 100f, 1 });
        Check(Math.Abs(before - near.Hp - 100f * Game.HorseSallyMult) < 0.5f, "riders deal 25% more for 4 s (" + (before - near.Hp).ToString("0.0") + ")");
        step(Game.HorseSallyTime);
        before = near.Hp;
        hurt.Invoke(g, new object[] { near, 100f, 1 });
        Check(Math.Abs(before - near.Hp - 100f) < 0.5f, "then back to normal");

        // --- Mastered, and the wave ending ---------------------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.TrojanHorse] = 1e6f;
        cast();
        Check(horse() != null && Math.Abs(horse().SoakMax - 1400f * Game.HorseHpMastered) < 0.5f && horse().Cap, "mastered: 40% more health");
        me.MasteryXp.Remove(AbilityId.TrojanHorse);
        reset();
        cast();
        z = horse();
        mePos = z.Pos;
        step(dt);
        g.HorseInteract();
        g.State = MatchState.Intermission;
        step(dt * 2f);
        Check(!Game.Riding(me) && horse() == null && me.SallyT == 0f, "between waves it folds away and you step out");
        g.State = MatchState.Fighting;
        host.Leave(""); client.Leave("");
    }
}
