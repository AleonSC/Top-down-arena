using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Astral Projection (AstralProjection.cs): key 1 of the Healer Ninja's Ultimate row reaching the run; the cast (8 s, the nearest
// teammate is the host); the body can't be hurt, can't move or dodge, and enemies drop it; the host heals 7 HP/s and moves 35%
// faster (on their own game too); the spirit leans toward the aim inside the tether; Fire shoots astral bolts from the spirit
// (6 a second, 9 damage, the weapon put away); right click switches teammate (once a press, round the team); a host going down
// passes the spirit on; alone it rides your own body; it ends on time and on going down; mastered and Astral Bond; the bot's rule.
static partial class Program
{
    static void AstralProjectionTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var pickTarget = typeof(Game).GetMethod("PickTarget", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        // --- The lobby: key 1 of the Ultimate row ----------------------------------------------------------------------
        var def = Upgrades.Ability(AbilityId.AstralProjection);
        Check(def.Name == "Astral Projection" && def.Cooldown == 70f && def.Choice == AbilityDef.Ult && def.UltKey == 1 && def.Role == Role.Healer && def.Kit == Kit.Ninja,
            "Astral Projection is the Healer Ninja's ultimate on key 1, 70 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Ninja) == AbilityId.BlossomStorm && Upgrades.Ultimate(Role.Healer, Kit.Ninja, 1) == AbilityId.AstralProjection,
            "Blossom Storm stays the default, Astral Projection is key 1");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Ninja, 1) == AbilityId.ForbiddenPath && Upgrades.Ultimate(Role.Tank, Kit.Ninja, 1) == AbilityId.DragonsDogma,
            "the other Ninja roles' key 1 is unchanged");

        int port = nextPort++;
        var host = new Session { AutoStart = 3, StartWave = 3 };
        host.Host(port, "Astral", Role.Healer, 0, 0, Kit.Ninja, 0, 1);
        var c1 = new Session();
        c1.Join("127.0.0.1", port, "Near", Role.Tank, 0, 0, Kit.Soldier, 0, 0);
        var c2 = new Session();
        c2.Join("127.0.0.1", port, "Far", Role.Dps, 0, 0, Kit.Knight, 0, 0);
        var s = new[] { host, c1, c2 };
        Run(s, dt, 3, () => host.Lobby.Count == 3 && c1.Lobby.Count == 3 && c2.Lobby.Count == 3 && !host.Game.Running);
        Run(s, dt, 2, () => host.Lobby[1].Ult == 1);
        Check(host.Lobby[1].UltId == AbilityId.AstralProjection, "the pick reaches the host");
        host.StartMatch();
        WaitForGame(host, c1, dt);
        Run(s, dt, 4, () => c2.Game.Running);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        Check(g.Players[1].Slots[Upgrades.UltSlot].Id == AbilityId.AstralProjection, "in the run the caster's ultimate is Astral Projection");

        var me = g.Players[1];
        int idNear = c1.Game.MyId, idFar = c2.Game.MyId;
        var near = g.Players[idNear];
        var far = g.Players[idFar];
        g.DevSetGod(idNear, true); g.DevSetGod(idFar, true);
        V2 mePos = new V2(500f, 450f), nearPos = new V2(620f, 450f), farPos = new V2(900f, 300f), aimAt = new V2(1100f, 450f);
        var mine = new HashSet<int>();
        Func<V2, Enemy> make = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FrozenT = 999f; mine.Add(e.Id); return e;
        };
        bool fire = false, parry = false;
        Action<float> step = secs =>
        {
            int n = Math.Max(1, (int)(secs / dt));
            for (int f = 0; f < n; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.Contains(k)).ToList()) g.Enemies.Remove(id);
                foreach (var id in g.Bullets.Values.Where(x => x.Owner != 1).Select(x => x.Id).ToList()) g.Bullets.Remove(id);
                host.Poll(); c1.Poll(); c2.Poll();
                me.Pos = mePos; near.Pos = nearPos; far.Pos = farPos;
                g.FixedStep(dt, new PlayerInput { AimAt = aimAt, Fire = fire, Parry = parry });
            }
        };
        Action reset = () =>
        {
            mine.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
            var sq = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
            while (sq.Count < 400) sq.Add(EnemyType.Grunt);
            foreach (var p in new[] { me, near, far }) { p.Downed = false; p.Hp = p.MaxHp; p.ApLinkT = 0f; }
            me.InvulnT = 0f; me.UltT = 0f; me.ApT = 0f; me.ApUp = false; me.ApBolts = me.ApSwaps = 0; me.ApHealed = 0f;
            me.Stats.ApBonusTime = 0f; me.Stats.ApDmgMult = 1f;
            me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; me.Pos = mePos; near.Pos = nearPos; far.Pos = farPos; masteredField.SetValue(g, false);
            fire = parry = false;
            g.State = MatchState.Fighting;
        };
        Action cast = () => hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, mePos, false });

        // --- The cast ---------------------------------------------------------------------------------------------------
        reset();
        float meSpeed0 = me.Speed, nearSpeed0 = near.Speed;
        var chaser = make(mePos + new V2(-200f, 0f));
        chaser.TargetId = 1; chaser.RetargetT = 5f;
        cast();
        Check(Math.Abs(me.ApT - Game.ApTime) < 1e-3f && me.UltT == me.ApT && Game.ApOn(me), "casting: 8 s");
        Check(me.ApHost == idNear && Game.ApAway(me), "the spirit rides the nearest teammate");
        Check(chaser.RetargetT <= 0f, "an enemy after the body looks for someone else");
        bool picked = false;
        for (int i = 0; i < 200; i++)
            picked |= (int)pickTarget.Invoke(g, null) == 1;
        Check(!picked, "enemies never pick the body");
        step(0.5f);
        Check(c1.Game.Players[1].ApT > 0f && c1.Game.Players[1].ApHost == idNear, "other games see the spirit and its host");

        // --- The body ---------------------------------------------------------------------------------------------------
        float hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 60f, "test" });
        Check(me.Hp == hp0, "the body can't be hurt");
        Check(me.Speed == 0f, "the body can't move");
        Check(!me.ParryActive, "the parry key raises no shield");

        // --- The host: heal and speed -----------------------------------------------------------------------------------
        Check(near.ApLinkT > 0f && Math.Abs(near.Speed / nearSpeed0 - Game.ApSpeedMult) < 1e-3f, "the host moves 35% faster");
        Check(c1.Game.Players[idNear].ApLinkT > 0f, "the host's own game sees the tether (for its speed)");
        Check(far.ApLinkT == 0f && Math.Abs(far.Speed - g.Players[idFar].Speed) < 1e-3f, "nobody else is sped up");
        near.Hp = near.MaxHp * 0.4f;
        float nh = near.Hp, healed0 = me.ApHealed;
        step(1f);
        Check(near.Hp - nh >= Game.ApHeal - 0.6f && Math.Abs(me.ApHealed - healed0 - Game.ApHeal) < 0.6f, "the host heals 7 HP a second, credited to the caster");

        // --- The spirit: lean and bolts ---------------------------------------------------------------------------------
        V2 lean = me.ApPos - near.Pos;
        Check(lean.Length <= Game.ApLean + 1f && lean.Length > Game.ApLean * 0.8f && lean.X > 0f, "the spirit leans toward the aim, at most 110 px from the host");
        var target = make(near.Pos + new V2(300f, 0f));
        fire = true;
        step(1f);
        fire = false;
        int bolts = me.ApBolts;
        Check(bolts >= 5 && bolts <= 7, "Fire shoots about 6 astral bolts a second (" + bolts + ")");
        Check(g.Bullets.Values.Where(b => b.Owner == 1).All(b => b.Kind == Defs.AstralBolt), "only astral bolts: the weapon is put away");
        step(0.6f);
        float dealt = target.MaxHp - target.Hp;
        Check(dealt >= Game.ApDamage * me.Stats.DmgMult * 3f, "the bolts hit what the spirit aims at (" + dealt + ")");

        // --- Switching --------------------------------------------------------------------------------------------------
        step(0.5f);
        parry = true;
        step(0.1f);
        Check(me.ApHost == idFar && me.ApSwaps == 1, "right click sends the spirit to the next teammate");
        step(0.6f);
        Check(me.ApHost == idFar && me.ApSwaps == 1, "holding it switches once");
        parry = false;
        step(0.1f);
        Check(far.ApLinkT > 0f && near.ApLinkT == 0f, "the new host is sped up, the old one not");
        parry = true;
        step(0.1f);
        parry = false;
        Check(me.ApHost == idNear && me.ApSwaps == 2, "it goes round the team");

        // --- A host going down, then nobody left ------------------------------------------------------------------------
        near.Downed = true;
        step(0.1f);
        Check(me.ApHost == idFar, "a host going down passes the spirit on");
        far.Downed = true;
        step(0.2f);
        Check(me.ApHost == 1 && !Game.ApAway(me) && me.ApLinkT > 0f, "with everyone down it rides your own body");
        Check(Math.Abs(me.Speed / meSpeed0 - Game.ApSpeedMult) < 1e-3f, "then your body is the host: it moves 35% faster");
        hp0 = me.Hp;
        damage.Invoke(g, new object[] { me, 60f, "test" });
        Check(me.Hp == hp0, "and still can't be hurt");
        near.Downed = far.Downed = false;
        parry = true;
        step(0.1f);
        parry = false;
        Check(me.ApHost != 1, "with a teammate up again, right click rides them");

        // --- The end ----------------------------------------------------------------------------------------------------
        me.ApT = 0.05f;
        step(0.4f);
        Check(me.ApT == 0f && me.UltT == 0f && !Game.ApOn(me) && Math.Abs(me.Speed - meSpeed0) < 1e-3f, "it ends on time and the body walks again");
        reset();
        cast();
        step(0.1f);
        me.Downed = true;
        step(0.1f);
        Check(me.ApT == 0f, "going down ends it at once");

        // --- Mastered, Astral Bond, the bot ----------------------------------------------------------------------------
        reset();
        me.MasteryXp[AbilityId.AstralProjection] = 1e6f;
        cast();
        Check(Math.Abs(me.ApT - Game.ApTimeMastered) < 1e-3f && me.ApMastered, "mastered: 10 s");
        me.MasteryXp.Remove(AbilityId.AstralProjection);
        reset();
        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Astral Bond");
        Check(card != null && card.RequiresAbility == AbilityId.AstralProjection && card.Rarity == Rarity.Rare, "Astral Bond is a Rare card for Astral Projection");
        card.Apply(me.Stats);
        cast();
        Check(Math.Abs(me.ApT - Game.ApTime - Game.ApBondTime) < 1e-3f && Math.Abs(me.Stats.ApDmgMult - 1.25f) < 1e-4f, "Astral Bond: 2 s longer, bolts 25% harder");
        reset();
        Check(def.Bot(new BotSense { AllyHurt = true }) && !def.Bot(new BotSense()), "a bot casts it when someone is hurt");

        foreach (var x in s) x.Leave("");
    }
}
