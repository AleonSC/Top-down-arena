using System.Linq;
using System;
using Arena;

// Chalice of Life (ChaliceOfLife.cs): the Healer Knight's key 3 on the lobby's Ultimate row (the pick reaching the run on F);
// the chalice drops up to 140 px toward the cursor and stands 12 s; walking up to it drinks once: wounds cleared, full health,
// 20 health past max and a 40 shield for 4 s; a second visit does nothing, a downed player can't drink, and it is gone once
// everyone has drunk; mastered, a 60 shield for 6 s.
static partial class Program
{
    static void ChaliceOfLifeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        var def = Upgrades.Ability(AbilityId.ChaliceOfLife);
        Check(def.Name == "Chalice of Life" && def.Cooldown == 90f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Kit == Kit.Knight && def.Role == Role.Healer && def.Capstone != null,
            "Chalice of Life is the Healer Knight's ultimate on key 3, 90 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Knight, 2) == AbilityId.HolyLanceRain && Upgrades.Ultimate(Role.Healer, Kit.Knight, 3) == AbilityId.ChaliceOfLife,
            "key 2 stays Holy Lance Rain; key 3 is Chalice of Life");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Paladin", Role.Healer, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].Ult == 3 && client.Lobby[1].Ult == 3 && host.Lobby[1].UltId == AbilityId.ChaliceOfLife, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.ChaliceOfLife && client.Game.Players[1].Slots[U].Id == AbilityId.ChaliceOfLife, "in the match the host's F is Chalice of Life, on every game");
        g.DevSetGod(1, true);

        V2 mePos = new V2(500f, 450f), allyPos = new V2(1400f, 200f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                g.Enemies.Clear(); g.Bullets.Clear();
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = mePos + new V2(400f, 0f) });
            }
        };
        Action hurt = () =>
        {
            foreach (var q in new[] { me, ally }) { q.Hp = q.MaxHp * 0.3f; q.Wound = q.MaxHp * 0.2f; q.OverHp = 0f; q.ShieldHp = 0f; q.ShieldT = 0f; q.Downed = false; }
        };
        Func<FxKind, int> count = k => g.Effects.Count(e => e.Kind == k);

        // --- The drop -----------------------------------------------------------------------------------------------------
        hurt();
        me.Pos = mePos;
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, U, mePos + new V2(400f, 0f) });
        var fx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.ChaliceOfLife);
        Check(fx != null && (int)fx.Radius == me.Id && Math.Abs(fx.Pos.DistanceTo(mePos) - Game.ChReach) < 1f && g.Sounds.Any(c => c.Sfx == Sfx.SpellHoly),
            "casting it drops the chalice 140 px toward the cursor (aimed 400 px away), on every screen, with a holy sound");
        V2 at = fx.Pos;
        step(1f);
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.ChaliceOfLife && e.Pos.DistanceTo(at) < 1f), "the other screen draws it");
        Check(me.Hp < me.MaxHp * 0.5f && count(FxKind.ChaliceSip) == 0, "standing 140 px away, nobody drinks");

        // --- Drinking -----------------------------------------------------------------------------------------------------
        mePos = at + new V2(-30f, 0f);
        step(dt * 3);
        Console.WriteLine("  chalice: hp {0:0}/{1:0}, wound {2:0}, over {3:0}, shield {4:0} for {5:0.0} s", me.Hp, me.MaxHp, me.Wound, me.OverHp, me.ShieldHp, me.ShieldT);
        Check(me.Wound == 0f && me.Hp >= me.MaxHp - 0.01f, "walking up to it clears your wounds and fills your health");
        Check(Math.Abs(me.OverHp - Game.ChOver) < 0.01f, "and gives 20 health past max");
        Check(Math.Abs(me.ShieldHp - Game.ChShield) < 0.01f && me.ShieldT > Game.ChShieldT - 0.2f, "and a 40 shield for 4 s");
        Check(count(FxKind.ChaliceSip) == 1 && g.Sounds.Any(c => c.Sfx == Sfx.Revive), "the sip is sent to every screen with a sound");
        Check(me.Run.HealingDone > 0f, "the healing counts for the knight");

        me.Hp = me.MaxHp * 0.5f; me.Wound = 10f;
        step(0.5f);
        Check(me.Hp < me.MaxHp * 0.6f && count(FxKind.ChaliceSip) == 1, "a second visit does nothing: one sip each");

        step(Game.ChShieldT);
        Check(me.ShieldHp == 0f && me.OverHp > 0f, "the shield is gone after 4 s; the health past max stays");

        // the ally, downed, can't drink; back up, they can, and then it's gone
        ally.Downed = true;
        allyPos = at + new V2(20f, 10f);
        step(0.3f);
        Check(count(FxKind.ChaliceSip) == 1, "a downed player can't drink");
        ally.Downed = false; ally.Hp = ally.MaxHp * 0.2f; ally.Wound = ally.MaxHp * 0.3f;
        step(dt * 3);
        Check(ally.Wound == 0f && ally.Hp >= ally.MaxHp - 0.01f && ally.ShieldHp > 0f && count(FxKind.ChaliceSip) == 2, "an ally drinks just the same");
        var list = (System.Collections.IList)typeof(Game).GetField("chalices", flags).GetValue(g);
        Check(list.Count == 0, "once everyone has drunk, the chalice is gone");

        // --- Its time running out, and mastered -----------------------------------------------------------------------------
        mePos = new V2(500f, 450f); allyPos = new V2(1400f, 200f);
        g.Effects.Clear();
        hurt();
        me.Pos = mePos;
        cast.Invoke(g, new object[] { me, U, mePos + new V2(400f, 0f) });
        masteredField.SetValue(g, false);
        step(Game.ChLife + 0.2f);
        Check(list.Count == 0, "after 12 s the chalice is gone, even with nobody drinking");
        hurt();
        me.Pos = mePos;
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, mePos });
        masteredField.SetValue(g, false);
        step(Game.ChDropT + dt * 3);
        Check(Math.Abs(me.ShieldHp - Game.ChShieldMastered) < 0.01f && me.ShieldT > Game.ChShieldTMastered - 0.5f, "mastered, the shield is 60 for 6 s");
    }
}
