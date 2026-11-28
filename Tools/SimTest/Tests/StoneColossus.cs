using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Stone Colossus (StoneColossus.cs): key 2 of the Tank Elementalist's Ultimate row reaching the run and a save; the rise (pillars, can't
// be hurt, no slams yet); the golem's 70% cut, speed and taunt; a stomp every stride; the slam (damage, stun, the weapon put away, one a
// second); the crumble's boulders; ending on time and on going down; mastered and Bedrock; the bot's rule; other screens seeing it.
static partial class Program
{
    static void StoneColossusTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 2 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.StoneColossus);
        Check(def.Name == "Stone Colossus" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Tank && def.Kit == Kit.Elementalist,
            "Stone Colossus is the Tank Elementalist's ultimate on key 2, 80 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Elementalist) == AbilityId.GlacialTomb && Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 1) == AbilityId.LightningRod
            && Upgrades.Ultimate(Role.Tank, Kit.Elementalist, 2) == AbilityId.StoneColossus && Upgrades.AltUltimate(Role.Tank, Kit.Elementalist, 3) == AbilityId.Vortex,
            "Glacial Tomb stays the default, Lightning Rod key 1, Stone Colossus key 2, Vortex key 3 (Vortex.cs)");
        Check(Upgrades.ClampUlt(Role.Tank, Kit.Elementalist, 2) == 2 && Content.UltFallsBack(2),
            "key 2 is Stone Colossus for the tank; a role or class without a key 2 falls back to its default");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Golem", Role.Tank, 0, 0, Kit.Elementalist, 0, 2);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0, 0, Kit.Elementalist, 0, 2);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2 && !host.Game.Running);
        int cid = host.Lobby.Values.First(e => e.Id != 1).Id;
        Run(s, dt, 2, () => host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2);
        Check(host.Lobby[1].Ult == 2 && host.Lobby[cid].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.StoneColossus, "the pick reaches the host and the other player");
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.StoneColossus && client.Game.Players[cid].Slots[Upgrades.UltSlot].Id == AbilityId.StoneColossus,
            "in the run both players' F is Stone Colossus, on both games");
        var save = new RunSave { Wave = 4 };
        save.Seats.Add(new SeatSave { Name = "Golem", Role = Role.Tank, Kit = Kit.Elementalist, Ult = 2 });
        int freeKey;
        var bare = Content.FindFreeUltKey(out freeKey);  // (a role and class with some key empty, if any is left)
        if (bare != null)
            save.Seats.Add(new SeatSave { Name = "Bare", Role = bare.Value.Role, Kit = bare.Value.Kit, Ult = freeKey });
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats[0].Ult == 2 && (bare == null ? Content.NoFreeUltKey() : back.Seats[1].Ult == 0), "a saved run keeps key 2 (and clamps a key a class doesn't have)");

        // --- The golem on the host's game -------------------------------------------------------------------------------
        var me = g.Players[1];
        var ally = g.Players[cid];
        g.DevSetGod(cid, true);
        V2 mePos = new V2(500f, 450f), aimAt = new V2(900f, 450f);
        var mine = new HashSet<int>();
        Func<V2, EnemyType, Enemy> make = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = e.StunT = 999f; mine.Add(e.Id); return e;  // (held by a stun, not ice: Brittle would shatter ice)
        };
        bool fire = false;
        Func<int, V2> where = f => mePos;
        Action<float> step = secs =>
        {
            int n = (int)(secs / dt);
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                me.Pos = where(f); ally.Pos = new V2(200f, 200f);
                foreach (var e in g.Enemies.Values) e.BrittleT = 0f;  // (the dummies are held in ice: no Brittle shatter or bonus in the numbers; elementstatus tests it)
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt, Fire = fire });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            me.Hp = me.MaxHp; me.Downed = false; me.InvulnT = 0f; me.UltT = 0f; me.ColT = 0f; me.ColUp = false; me.ColWind = 0f; me.ColSlamT = -1f; me.ColStomps = me.ColSlams = 0;
            me.Stats.ColBonusTime = 0f; me.Stats.ColSlamWide = 0f; me.TauntT = 0f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; masteredField.SetValue(g, false);
            fire = false; where = f => mePos;
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
        Check(Math.Abs(me.ColT - (Game.ColRise + Game.ColTime)) < 1e-3f && me.UltT == me.ColT && me.InvulnT >= Game.ColRise, "casting: 1 s rise + 15 s, and you can't be hurt while it forms");
        Check(Math.Abs(near.MaxHp - near.Hp - Game.ColRiseDamage * dmg) < 0.5f && far.Hp == far.MaxHp, "the pillars hurt enemies close by (20), not far ones");
        Check(far.TargetId == 1 && (int)pickTarget.Invoke(g, null) == 1, "every enemy turns on you");
        Check(Math.Abs(me.Speed / speed0 - Game.ColMoveRising) < 1e-4f, "you're slow while it forms");
        var front = make(mePos + new V2(Game.ColSlamAhead, 0f), EnemyType.Grunt);
        fire = true;
        step(0.5f);
        Check(me.ColSlams == 0 && front.Hp == front.MaxHp, "no slams during the rise");
        fire = false;
        step(0.6f);
        Check(Game.ColStanding(me) && client.Game.Players[1].ColT > 0f && client.Game.Players[1].ColAge > Game.ColRise, "it stands after 1 s; other screens see it");
        Check(Math.Abs(me.Speed / speed0 - Game.ColMove) < 1e-4f, "the golem walks at 80%");

        // The 70% cut.
        me.InvulnT = 0f; float hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        float cut = hp0 - me.Hp;
        float keep = me.ColT; me.ColT = 0f; me.InvulnT = 0f; hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 40f, "test" });
        float plain = hp0 - me.Hp;
        me.ColT = keep; me.Hp = me.MaxHp; me.InvulnT = 0f;
        Console.WriteLine("  colossus: 40 damage -> {0:0.0} as the golem, {1:0.0} without", cut, plain);
        Check(plain > 0f && Math.Abs(cut / plain - (1f - Game.ColCut)) < 0.02f, "the golem takes 70% less damage");

        // The slam: Fire, the weapon put away.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        V2 slamAt = mePos + new V2(Game.ColSlamAhead, 0f);
        var hitA = make(slamAt, EnemyType.Grunt);
        var hitB = make(slamAt + new V2(90f, 0f), EnemyType.Grunt);
        var miss = make(slamAt + new V2(170f, 0f), EnemyType.Grunt);
        hitA.FrozenT = hitB.FrozenT = miss.FrozenT = 0f;
        g.Bullets.Clear();
        fire = true;
        bool windSeen = false;
        for (int i = 0; i < 12; i++) { step(dt); windSeen |= client.Game.Players[1].ColWind > 0f; }
        step(0.12f);
        Check(me.ColSlams == 1 && Math.Abs(hitA.MaxHp - hitA.Hp - Game.ColSlamDamage * dmg) < 0.5f && Math.Abs(hitB.MaxHp - hitB.Hp - Game.ColSlamDamage * dmg) < 0.5f && miss.Hp == miss.MaxHp,
            "a slam lands 110 px ahead: 60 to everything within 110 px, nothing beyond");
        Check(hitA.StunT > 0.3f, "the slam stuns");
        Check(windSeen, "other screens see the fists wind up");
        step(1.1f);
        Check(me.ColSlams == 2, "holding Fire slams once a second");
        Check(!g.Bullets.Values.Any(b => b.Owner == 1), "the weapon doesn't fire while you're the golem");
        fire = false;

        // Stomps: a foot comes down every 40 px walked.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        var under = make(mePos + new V2(120f, 0f), EnemyType.Grunt);
        int stomps0 = me.ColStomps;
        where = f => mePos + new V2(Math.Min(240f, f * 1.6f), 0f);
        step(2.6f);
        int stomps = me.ColStomps - stomps0;
        float trod = under.MaxHp - under.Hp;
        Console.WriteLine("  colossus: walked 240 px -> {0} stomps, the grunt in the way took {1:0}", stomps, trod);
        Check(stomps == 6, "6 stomps for 240 px");
        Check(trod >= Game.ColStompDamage * dmg - 0.5f && Math.Abs(trod / (Game.ColStompDamage * dmg) - Math.Round(trod / (Game.ColStompDamage * dmg))) < 0.02f, "an enemy in your path is stomped (25 a footfall)");
        Check(g.Effects.Any(e => e.Kind == FxKind.TitanSlam && (int)(e.Pos2.X + 0.5f) == Game.ColFxStomp), "each footfall leaves its mark");
        where = f => mePos;
        step(0.05f);

        // The crumble on time: the boulders roll out.
        foreach (var id in mine.ToList()) g.Enemies.Remove(id);
        mine.Clear();
        var ring = new List<Enemy>();
        for (int i = 0; i < 16; i++)
            ring.Add(make(mePos + V2.FromAngle(MathX.Tau * i / 16f) * 150f, EnemyType.Grunt));
        me.ColT = 0.05f;
        step(0.1f);
        Check(me.ColT == 0f && me.UltT == 0f && !Game.ColOn(me), "it ends on time");
        step(1f);
        int rolled = ring.Count(e => Math.Abs(e.MaxHp - e.Hp - Game.ColBoulderDamage * dmg) < 0.5f);
        Console.WriteLine("  colossus: the crumble's boulders ran over {0} of 16 enemies round you", rolled);
        Check(rolled >= 8, "8 boulders roll out, 40 to each enemy they run over");
        Check(Math.Abs(me.Speed / speed0 - 1f) < 1e-4f, "your speed is back");

        // Going down ends it.
        reset();
        cast();
        step(1.2f);
        me.Downed = true;
        step(0.05f);
        Check(me.ColT == 0f, "going down ends it at once");
        me.Downed = false; me.Hp = me.MaxHp;

        // Mastered and Bedrock.
        reset();
        me.MasteryXp[AbilityId.StoneColossus] = 1e6f;
        cast();
        Check(Math.Abs(me.ColT - (Game.ColRise + Game.ColTimeMastered)) < 1e-3f && Math.Abs(Game.ColSlamReach(me) - Game.ColSlamRadius * 1.25f) < 0.01f, "mastered: 18 s and slams 25% wider");
        me.MasteryXp.Remove(AbilityId.StoneColossus);
        reset();
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Bedrock");
        Check(card != null && card.RequiresAbility == AbilityId.StoneColossus && card.Rarity == Rarity.Rare, "Bedrock is a Rare card for Stone Colossus");
        card.Apply(me.Stats);
        cast();
        Check(Math.Abs(me.ColT - (Game.ColRise + Game.ColTime + Game.ColBedrockTime)) < 1e-3f && Math.Abs(Game.ColSlamReach(me) - Game.ColSlamRadius * 1.25f) < 0.01f, "Bedrock: 3 s longer, slams 25% wider");
        reset();

        // The bot's rule.
        Check(def.Bot(new BotSense { Me = me, Crowd = Game.JuggBotCrowd }) && !def.Bot(new BotSense { Me = me, Crowd = 1 }), "a bot becomes the golem in a crowd");

        host.Leave(""); client.Leave("");
    }
}
