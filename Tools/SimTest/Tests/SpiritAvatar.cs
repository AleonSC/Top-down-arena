using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Spirit Avatar (SpiritAvatar.cs): key 2 of the Tank Ninja's Ultimate row reaching the run; the rise (can't be hurt, a shove, no
// cleaves yet); the 60% cut, speed and taunt; shots swallowed; the Moon Cleave (the wind-up seen on other games, the sweep's arc and
// reach, damage, stun and push, heavies half, bosses unpushed, the weapon put away, one every 1.6 s); the release on time and on going
// down; mastered and Kami Vessel; the bot's rule.
static partial class Program
{
    static void SpiritAvatarTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.SpiritAvatar);
        Check(def.Name == "Spirit Avatar" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Tank && def.Kit == Kit.Ninja,
            "Spirit Avatar is the Tank Ninja's ultimate on key 2, 70 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Ninja) == AbilityId.ThousandClones && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 1) == AbilityId.DragonsDogma
            && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 2) == AbilityId.SpiritAvatar, "Thousand Clones stays the default, Dragon's Dogma key 1, Spirit Avatar key 2");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Ninja, 2) == 2 && Upgrades.Ultimate(Role.Dps, Kit.Ninja, Upgrades.ClampUlt(Role.Dps, Kit.Ninja, 2)) != AbilityId.SpiritAvatar,
            "key 2 is Spirit Avatar only for the tank");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Spirit", Role.Tank, 0, 0, Kit.Ninja, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Ninja, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].UltId == AbilityId.SpiritAvatar, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.SpiritAvatar && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.SpiritAvatar,
            "in the run both players' ultimate is Spirit Avatar, on both games");

        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(500f, 450f), aimAt = new V2(900f, 450f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; mine.Add(e.Id); return e;
        };
        bool fire = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1 && x.Id < 90000).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = mePos; ally.Pos = new V2(200f, 200f);
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt, Fire = fire });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.UltT = 0f; me.SpT = 0f; me.SpUp = false; me.SpWind = 0f; me.SpCastT = -1f; me.SpCleaves = me.SpSwallowed = 0;
            me.Stats.SpBonusTime = 0f; me.Stats.SpWindCut = 0f; me.TauntT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            fire = false;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });
        float dmg = me.Stats.DmgMult;

        // The rise.
        reset();
        float speed0 = me.Speed;
        var near = make(mePos + new V2(60f, 0f), EnemyType.Grunt);
        var far = make(mePos + new V2(380f, 120f), EnemyType.Grunt);
        far.TargetId = cid; far.RetargetT = 5f;
        cast();
        Check(Math.Abs(me.SpT - (Game.SpRise + Game.SpTime)) < 1e-3f && me.UltT == me.SpT && me.InvulnT >= Game.SpRise, "casting: 0.8 s rise + 12 s, and you can't be hurt while it forms");
        Check(Math.Abs(near.MaxHp - near.Hp - Game.SpRiseDamage * dmg) < 0.5f && far.Hp == far.MaxHp, "the rise hurts enemies close by (30), not far ones");
        Check(far.TargetId == 1 && (int)pickTarget.Invoke(g, null) == 1, "every enemy turns on you");
        Check(Math.Abs(me.Speed / speed0 - Game.SpMoveRising) < 1e-4f, "you're slow while it forms");
        fire = true;
        step(0.5f);
        Check(me.SpCleaves == 0 && me.SpWind == 0f, "no cleaves during the rise");
        fire = false;
        step(0.4f);
        Check(Game.SpStanding(me) && client.Game.Players[1].SpT > 0f && client.Game.Players[1].SpAge > Game.SpRise, "it stands after 0.8 s; other screens see it");
        Check(Math.Abs(me.Speed / speed0 - Game.SpMove) < 1e-4f, "you walk at 85%");

        // The 60% cut.
        me.InvulnT = 0f; float hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        float cut = hp0 - me.Hp;
        float keep = me.SpT; me.SpT = 0f; me.InvulnT = 0f; hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        float plain = hp0 - me.Hp;
        me.SpT = keep; me.Hp = me.MaxHp; me.InvulnT = 0f;
        Console.WriteLine("  spirit: 40 damage -> {0:0.0} in the spirit, {1:0.0} without", cut, plain);
        Check(plain > 0f && Math.Abs(cut / plain - (1f - Game.SpCut)) < 0.02f, "the spirit takes 60% less damage");

        // Shots that reach the body are swallowed; ones outside it fly on.
        g.Bullets[95001] = new Bullet { Id = 95001, Kind = Defs.SmallShot, Pos = mePos + new V2(0f, -50f), Radius = 4f, Life = 5f, Vel = new V2(0f, 10f), Dmg = 10f };
        g.Bullets[95002] = new Bullet { Id = 95002, Kind = Defs.SmallShot, Pos = mePos + new V2(0f, -200f), Radius = 4f, Life = 5f, Vel = new V2(0f, -10f), Dmg = 10f };
        step(dt * 2f);
        Check(!g.Bullets.ContainsKey(95001) && g.Bullets.ContainsKey(95002) && me.SpSwallowed == 1, "an enemy shot inside the spirit's body is swallowed, one outside isn't");
        Check(g.Effects.Any(e => e.Kind == FxKind.SpiritInk && (int)(e.Pos2.X + 0.5f) == Game.SpFxSwallow), "with an ink splash");
        g.Bullets.Remove(95002);

        // The Moon Cleave: Fire, a slow wind-up, then a 200° sweep out to 170 px.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        var ahead = make(mePos + new V2(120f, 0f), EnemyType.Grunt);
        var side = make(mePos + V2.FromAngle(1.5f) * 140f, EnemyType.Grunt);  // 86°: inside the arc
        var behind = make(mePos + new V2(-120f, 0f), EnemyType.Grunt);        // outside the arc
        var past = make(mePos + new V2(Game.SpReach + 60f, 0f), EnemyType.Grunt);  // beyond its reach
        var brute = make(mePos + V2.FromAngle(-0.8f) * 110f, EnemyType.Thunderhead);  // heavy
        V2 aheadAt = ahead.Pos, bruteAt = brute.Pos;
        g.Bullets.Clear();
        fire = true;
        bool windSeen = false;
        int windSteps = (int)(Game.SpWindup * 0.8f / dt);
        for (int i = 0; i < windSteps; i++) { step(dt); windSeen |= client.Game.Players[1].SpWind > 0f && client.Game.Players[1].SpWind <= 1f; }
        Check(ahead.Hp == ahead.MaxHp && me.SpCleaves == 0, "nothing is hit during the wind-up");
        Check(windSeen, "other screens see the wind-up (the floor arc)");
        step(Game.SpWindup * 0.2f + Game.SpSweep + 0.05f);
        float hitDmg = Game.SpDamage * dmg;
        Check(me.SpCleaves == 1 && Math.Abs(ahead.MaxHp - ahead.Hp - hitDmg) < 0.5f && Math.Abs(side.MaxHp - side.Hp - hitDmg) < 0.5f, "the sweep hits everything in its 200° arc for 80");
        Check(behind.Hp == behind.MaxHp && past.Hp == past.MaxHp, "nothing behind you or beyond 170 px");
        Check(ahead.StunT > 0.2f && brute.StunT <= 0f, "a regular enemy is stunned, a heavy isn't");
        step(0.3f);
        float pushA = ahead.Pos.DistanceTo(aheadAt), pushB = brute.Pos.DistanceTo(bruteAt);
        Console.WriteLine("  spirit: the cleave pushed a grunt {0:0} px and a heavy {1:0} px", pushA, pushB);
        Check(pushA > Game.SpKnock * 0.7f && pushB > Game.SpKnock * 0.3f && pushB < pushA * 0.75f, "it throws enemies back ~150 px, heavies half as far");
        Check(g.Effects.Any(e => e.Kind == FxKind.SpiritInk && (int)(e.Pos2.X + 0.5f) == Game.SpFxStroke), "the stroke goes down on the floor");
        Check(!g.Bullets.Values.Any(b => b.Owner == 1), "the weapon doesn't fire while the spirit is out");
        step(Game.SpEvery + 0.2f);
        Check(me.SpCleaves == 2, "holding Fire cleaves again after 1.6 s");
        fire = false;

        // A boss takes the damage but isn't pushed.
        reset();
        cast();
        step(Game.SpRise + 0.05f);
        var boss = make(mePos + new V2(110f, 0f), EnemyType.Brute);
        boss.Def = Defs.Enemies.First(d => d.IsBoss);
        V2 bossAt = boss.Pos;
        fire = true;
        step(Game.SpWindup + Game.SpSweep + 0.3f);
        fire = false;
        Check(boss.Hp < boss.MaxHp && boss.Pos.DistanceTo(bossAt) < 20f, "a boss is hurt but not pushed");

        // The release on time.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        var close = make(mePos + new V2(100f, 30f), EnemyType.Grunt);
        var away = make(mePos + new V2(260f, 0f), EnemyType.Grunt);
        me.SpT = 0.05f;
        step(0.1f);
        Check(me.SpT == 0f && me.UltT == 0f && !Game.SpOn(me), "it ends on time");
        Check(Math.Abs(close.MaxHp - close.Hp - Game.SpReleaseDamage * dmg) < 0.5f && away.Hp == away.MaxHp, "the release bursts for 40 within 130 px");
        Check(g.Effects.Any(e => e.Kind == FxKind.SpiritInk && (int)(e.Pos2.X + 0.5f) == Game.SpFxRelease), "with its ink burst");
        Check(Math.Abs(me.Speed / speed0 - 1f) < 1e-4f, "your speed is back");

        // Going down ends it.
        reset();
        cast();
        step(1.2f);
        me.Downed = true;
        step(0.05f);
        Check(me.SpT == 0f, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered and Kami Vessel.
        reset();
        me.MasteryXp[AbilityId.SpiritAvatar] = 1e6f;
        cast();
        Check(Math.Abs(me.SpT - (Game.SpRise + Game.SpTimeMastered)) < 1e-3f && Math.Abs(Game.SpReachOf(me) - Game.SpReach * 1.2f) < 0.01f, "mastered: 15 s and the cleave reaches 20% further");
        me.MasteryXp.Remove(AbilityId.SpiritAvatar);
        reset();
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Kami Vessel");
        Check(card != null && card.RequiresAbility == AbilityId.SpiritAvatar && card.Rarity == Rarity.Rare, "Kami Vessel is a Rare card for Spirit Avatar");
        card.Apply(me.Stats);
        cast();
        Check(Math.Abs(me.SpT - (Game.SpRise + Game.SpTime + Game.SpVesselTime)) < 1e-3f && Math.Abs(Game.SpWindupOf(me) - (Game.SpWindup - Game.SpVesselWind)) < 1e-4f, "Kami Vessel: 2 s longer, the wind-up 0.2 s faster");
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.SpBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot calls the spirit in a crowd");

        host.Leave(""); client.Leave("");
    }
}
