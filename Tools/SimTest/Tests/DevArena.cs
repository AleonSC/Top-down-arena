using System;
using System.Linq;
using Arena;

// The F1 dock's Dev Arena (DevArena.cs): no waves, god mode, and playing as an enemy to try its attacks.
static partial class Program
{
    static void DevArenaTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        var cg = s[1].Game;
        g.Bot = cg.Bot = false;
        Run(s, dt, 8, () => g.State == MatchState.Fighting);
        Action calm = () => { foreach (var q in g.Players.Values) { q.PendingPicks = 0; q.Offer = null; } };

        cg.DevSetArena(true);
        Check(!cg.DevArena, "only the host turns the Dev Arena on");
        int wave = g.Wave;
        g.DevSetArena(true);
        Check(g.DevArena && g.Enemies.Count == 0 && g.Players.Values.All(p => p.DevGod), "the Dev Arena clears the field and puts everyone in god mode");
        Run(s, dt, 4f, null, calm);
        Check(g.State == MatchState.Fighting && g.Wave == wave && g.Enemies.Count == 0, "no enemies arrive and the empty wave doesn't clear");

        var me = g.Players[1];
        Check(g.DevPossess(1, EnemyType.Grunt) && g.Possessed != null && g.Possessed.Type == EnemyType.Grunt, "play as a Grunt");
        var e = g.Possessed;
        Run(s, dt, 0.5f, null, () => { calm(); g.SetPossessInput(V2.Zero, e.Pos, false, false); });
        Func<int> shots = () => g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
        g.Bullets.Clear();
        V2 start = e.Pos;
        float x0 = start.X < Game.Width / 2f ? 1f : -1f;
        Run(s, dt, 1f, null, () => { calm(); g.SetPossessInput(new V2(x0, 0f), e.Pos + new V2(0f, 300f), false, false); });
        Check((e.Pos.X - start.X) * x0 > e.Speed * 0.6f && Math.Abs(e.Pos.Y - start.Y) < 30f, "the move keys steer it (" + (e.Pos - start).Length.ToString("0") + " px)");
        Check(shots() == 0, "it holds fire until you attack");
        Run(s, dt, 3f, null, () => { calm(); g.SetPossessInput(V2.Zero, me.Pos, true, false); });
        Check(shots() > 0, "held Fire: it shoots on its own cadence");
        g.Bullets.Clear();
        e.FireT = 5f;
        g.SetPossessInput(V2.Zero, me.Pos, false, true);
        Run(s, dt, 0.1f, null, calm);
        Check(shots() > 0, "attack now skips the cooldown");
        Run(s, dt, 0.5f);
        Check(cg.Enemies.ContainsKey(e.Id), "the enemy you play shows on the other screens");

        // A Lunger lunges at the cursor.
        g.DevClearArena();
        Check(g.DevPossess(1, EnemyType.Lunger), "play as a Lunger");
        var l = g.Possessed;
        bool leapt = false;
        bool first = true;
        Run(s, dt, 1.5f, () => leapt, () => { calm(); g.SetPossessInput(V2.Zero, l.Pos + new V2(150f, 0f), true, first); first = false; leapt |= l.Phase == 2; });
        Check(leapt, "a Lunger played: attack now, then held, makes it lunge at the cursor");

        // Hero dummies: enemy shots and touches land on them, and other enemies aim at them.
        g.DevRelease();
        g.DevClearArena();
        V2 mid = new V2(Game.Width / 2f, Game.Height / 2f);
        me.Pos = mid + new V2(0f, 380f);
        Check(g.DevAddHeroDummy(mid) && g.HeroDummies.Count == 1, "a hero dummy goes down");
        var dummy = g.HeroDummies[0];
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var gr = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.Grunt, mid + new V2(-260f, 0f), 0f, 0, false });
        gr.SpawnT = 0f; gr.Speed = 0f;
        Run(s, dt, 5f, () => dummy.Hits > 0, calm);
        Check(dummy.Hits > 0 && dummy.Taken > 0f && me.Run.DamageTaken == 0f, "an ordinary Grunt shoots the dummy instead of you (" + dummy.Hits + " hits, " + dummy.Taken.ToString("0") + " damage)");
        Check(g.HeroDummyDps(dummy) > 0f, "the dummy shows its DPS");
        g.DevClearArena();
        Check(g.HeroDummies.Count == 1, "Clear arena keeps the dummies");
        Check(g.DevPossess(1, EnemyType.Brute), "play as a Brute");
        var br = g.Possessed;
        br.Pos = mid + new V2(-110f, 0f);
        int hits0 = dummy.Hits;
        Run(s, dt, 3f, () => dummy.Hits > hits0, () => { calm(); g.SetPossessInput((dummy.Pos - br.Pos).Normalized(), dummy.Pos, true, false); });
        Check(dummy.Hits > hits0, "walked into the dummy while attacking: its touch lands");
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        g.PossessUnkillable = true;
        hurt.Invoke(g, new object[] { br, br.MaxHp * 5f, 1 });
        Run(s, dt, 0.3f, null, calm);
        Check(g.Possessed == br && br.Hp == br.MaxHp, "can't die: a killing blow leaves it standing, healed up");
        g.PossessUnkillable = false;
        hurt.Invoke(g, new object[] { br, br.MaxHp * 5f, 1 });
        Run(s, dt, 0.3f, null, calm);
        Check(g.Possessed == null, "with can't die off it dies, and lets go");
        g.PossessUnkillable = true;
        for (int k = 0; k < 8; k++) g.DevAddHeroDummy(mid + new V2(k * 50f - 200f, -200f));
        Check(g.HeroDummies.Count == Game.MaxHeroDummies, "at most " + Game.MaxHeroDummies + " hero dummies");
        g.DevRemoveHeroDummy(mid + new V2(-200f, -200f));
        Check(g.HeroDummies.Count == Game.MaxHeroDummies - 1, "remove the dummy nearest a point");
        g.DevRemoveHeroDummy(V2.Zero, true);
        Check(g.HeroDummies.Count == 0, "clear the dummies");

        g.DevRelease();
        Check(g.Possessed == null && g.PossessId < 0, "let go");
        Check(g.DevPossess(1, EnemyType.Brute), "play as a Brute");
        g.Possessed.Hp = 0f;
        Check(g.Possessed == null, "a dead enemy lets go by itself");

        g.DevSetArena(false);
        g.DevClearArena();
        Run(s, dt, 6f, () => g.State != MatchState.Fighting, calm);
        Check(g.State != MatchState.Fighting && g.PossessId < 0, "leaving the Dev Arena: the empty wave clears as usual");
        s[0].Leave(""); s[1].Leave("");
    }
}
