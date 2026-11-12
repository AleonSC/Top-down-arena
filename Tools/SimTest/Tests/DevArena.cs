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
