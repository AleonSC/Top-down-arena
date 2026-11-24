using System;
using System.Collections.Generic;

namespace Arena
{
    /// One player locked in a Holding Cell (HoldingCell.cs). The host runs it; every game gets it in the snapshot
    /// (SyncHoldingCells) and runs the clock on between snapshots.
    public class HoldingCell
    {
        public int PlayerId;   // who's locked in
        public int Slot;       // which of the three cells (CellCentre)
        public float Left;     // seconds until the door opens
        public V2 At;          // where they were netted: the padlock in the arena, and where they come back
        public float Age;      // seconds since they were put in
        public int LockHits;   // the padlock's hits from a teammate (each takes CellLockCut off)
        public int Skills;     // abilities cast inside (each takes CellSkillCut off)
        public bool Out;       // the door's open: on the way back to the fight
        public float OutT;     // seconds since the door opened
        public float SpawnT;   // host: until the next guard comes in
        public bool Moved;     // host: sent back to the arena
        public int Sent;       // host: guards sent in so far
        public float LockAt = -9f;  // host: when its padlock last took a hit
    }

    /// The Warden's net and Holding Cells (Docs/Mockups/warden-net-cell.html). Caught by the searchlight, a player
    /// is netted (WardenState.NetId, Warden.cs), the screen fades and they're locked in a cell the size of one grid
    /// cell, far off the arena (CellCentre: three side by side, so a second player netted lands next door, in view).
    /// Riot Guards pile in through a gap in each wall, faster and faster; the door opens after CellTime, every
    /// ability cast inside takes CellSkillCut off, and a teammate shooting the padlock (at the spot they were netted)
    /// takes CellLockCut off each hit. Downed inside, they're sent back to the fight downed for a teammate to revive.
    /// While nobody is left fighting in the arena (solo, or everyone netted) the fight pauses (ArenaHeld): enemies
    /// and shots outside the cells hold still. The look is Game/HoldingCellLook.cs; test suite `holdingcell`.
    public partial class Game
    {
        public const float CellTime = 30f, CellSkillCut = 1f, CellLockCut = 0.25f;
        public const float CellHalf = 300f;             // one grid cell: 600 px square
        public const float CellPitch = 900f;            // slot to slot
        public const float CellGap = 46f;               // each wall's opening, half its width
        public const float CellZone = 140f;             // past the bars, a spot still counts as the cell's (and is pulled in)
        public const float CellLockR = 18f;             // the padlock's size as a target
        public const float CellLockGap = 0.1f;          // at most one padlock hit this often (a shotgun's pellets are one hit)
        public const float CellFadeIn = 0.5f;           // the screen fading in, held still
        public const float CellOutMove = 0.6f, CellOutEnd = 1.1f;  // the door opens: faded out and moved back, then faded in
        public const float CellShield = 1.5f;           // back in the fight: invulnerable this long
        public const int CellGuardCap = 10;
        public static readonly float CellY = 5000f;     // well below any arena
        static readonly int[] CellOrder = { 1, 0, 2 };  // the first cell in the middle, the next beside it

        /// The Holding Cells in use (everyone's game: the host's own, a client's from the snapshot).
        public readonly List<HoldingCell> HoldingCells = new List<HoldingCell>();

        readonly HashSet<int> cellHolds = new HashSet<int>();  // host: players whose WarpT the net or a cell holds

        public static string CellName(int slot) => "C-0" + (slot + 4);  // C-04, C-05, C-06

        public static V2 CellCentre(int slot) => new V2(Width / 2f + (slot - 1) * CellPitch, CellY);

        /// The cell a point is in (with `pad` past its bars), or -1. Fixed places, so it works before the snapshot says who's in.
        public static int CellSlotAt(V2 p, float pad)
        {
            if (p.Y < CellY - CellHalf - pad || p.Y > CellY + CellHalf + pad)
                return -1;
            for (int s = 0; s < 3; s++)
                if (Math.Abs(p.X - CellCentre(s).X) <= CellHalf + pad)
                    return s;
            return -1;
        }

        /// In a cell (or just past its bars, where anything is pulled back in).
        public static bool InCellZone(V2 p) => CellSlotAt(p, CellZone) >= 0;

        static V2 ClampToCell(int slot, V2 pos, float r)
        {
            V2 c = CellCentre(slot), h = new V2(CellHalf - r, CellHalf - r);
            return pos.Clamp(c - h, c + h);
        }

        /// The player's cell, if they're in one (null otherwise).
        public HoldingCell CellOf(Player p)
        {
            if (p == null)
                return null;
            foreach (var c in HoldingCells)
                if (c.PlayerId == p.Id)
                    return c;
            return null;
        }

        public HoldingCell CellInSlot(int slot)
        {
            foreach (var c in HoldingCells)
                if (c.Slot == slot)
                    return c;
            return null;
        }

        /// Locked in a cell (not on the way out): out of the fight, so enemies in the arena leave them be.
        public bool Jailed(Player p) => p != null && !p.Downed && InCellZone(p.Pos);

        /// Nobody left fighting in the arena: the fight holds still until someone comes back (solo, or everyone netted).
        public bool ArenaHeld
        {
            get
            {
                if (HoldingCells.Count == 0)
                    return false;
                bool anyIn = false;
                foreach (var p in Players.Values)
                {
                    if (p.Downed)
                        continue;
                    if (!InCellZone(p.Pos))
                        return false;
                    anyIn = true;
                }
                return anyIn;
            }
        }

        // --- The host --------------------------------------------------------------------------------

        /// Host, from the Warden's net (Warden.cs): lock them up.
        void JailPlayer(Player p, V2 at)
        {
            if (CellOf(p) != null)
                return;
            int slot = -1;
            foreach (int s in CellOrder)
                if (CellInSlot(s) == null)
                {
                    slot = s;
                    break;
                }
            if (slot < 0)
                return;
            var cell = new HoldingCell { PlayerId = p.Id, Slot = slot, Left = CellTime, At = at, SpawnT = 0.35f };
            HoldingCells.Add(cell);
            MovePlayer(p, CellCentre(slot) + new V2(0f, CellHalf * 0.35f));
            p.Disp = p.Pos;
            p.InvulnT = Math.Max(p.InvulnT, CellFadeIn + 0.3f);
            p.StunT = 0f;
            BroadcastSound(Sfx.BossCage);
            DoLog("the Warden locks player " + p.Id + " in Holding Cell " + slot);
        }

        /// Host, every tick: the cells' clocks, the guards coming in, the door, and anyone downed or left behind.
        void TickHoldingCells(float dt)
        {
            HoldNetted();
            if (HoldingCells.Count == 0)
                return;
            var warden = TheWarden();
            for (int i = HoldingCells.Count - 1; i >= 0; i--)
            {
                var cell = HoldingCells[i];
                Player p;
                if (!Players.TryGetValue(cell.PlayerId, out p))
                {
                    ClearCellGuards(cell.Slot, false);
                    HoldingCells.RemoveAt(i);
                    continue;
                }
                if (warden == null || !Running || State != MatchState.Fighting)
                {
                    FreeFromCell(cell, p, "the fight is over");  // the Warden's beaten (or the run ended): everyone back at once
                    continue;
                }
                if (p.Downed)
                {
                    FreeFromCell(cell, p, "downed inside");  // back to the fight downed, for a teammate to revive
                    continue;
                }
                cell.Age += dt;
                if (cell.Out)
                {
                    cell.OutT += dt;
                    if (!cell.Moved && cell.OutT >= CellOutMove)
                    {
                        cell.Moved = true;
                        V2 back = PushOutOfProps(ClampToArena(cell.At, p.Def.BodyR), p.Def.BodyR);
                        MovePlayer(p, back);
                        p.Disp = back;
                        p.InvulnT = Math.Max(p.InvulnT, CellShield);
                        BroadcastFx(FxKind.Ring, back, p.Def.Color, 70f, 0.5f, Sfx.Blink);
                    }
                    if (cell.OutT >= CellOutEnd)
                        HoldingCells.RemoveAt(i);
                    continue;
                }
                cell.Left -= dt;
                if (cell.Left <= 0f)
                {
                    cell.Left = 0f;
                    cell.Out = true;
                    cell.OutT = 0f;
                    ClearCellGuards(cell.Slot, true);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.82f, 0.35f), 160f, 0.5f, Sfx.BossCrack);  // the door swings open
                    DoLog("player " + p.Id + " is let out of Holding Cell " + cell.Slot);
                    continue;
                }
                if (cell.Age < CellFadeIn)
                    continue;
                cell.SpawnT -= dt;
                if (cell.SpawnT <= 0f)
                {
                    cell.SpawnT = Math.Max(0.55f, 1.6f - (cell.Age - CellFadeIn) * 0.04f);
                    int n = cell.Sent == 0 ? 4 : 1;  // the first four through every wall at once
                    for (int k = 0; k < n && CellGuardCount(cell.Slot) < CellGuardCap; k++, cell.Sent++)
                        CellGuard(cell.Slot, n == 4 ? k : rng.Next(4));
                }
            }
            HoldNetted();
        }

        /// A Riot Guard squeezing in through one wall's gap (0 top, 1 right, 2 bottom, 3 left).
        void CellGuard(int slot, int side)
        {
            V2 c = CellCentre(slot), dir = side == 0 ? new V2(0f, -1f) : side == 1 ? new V2(1f, 0f) : side == 2 ? new V2(0f, 1f) : new V2(-1f, 0f);
            V2 at = c + dir * (CellHalf - 24f) + dir.Orthogonal() * Rand(-CellGap * 0.6f, CellGap * 0.6f);
            SpawnEnemyAt(EnemyType.Guard, at, 0.45f, 0);
            BroadcastFx(FxKind.Ring, c + dir * CellHalf, Defs.Get(EnemyType.Guard).Color, 60f, 0.4f, Sfx.None);
        }

        int CellGuardCount(int slot)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (CellSlotAt(e.Pos, CellZone) == slot)
                    n++;
            return n;
        }

        /// Every enemy in that cell gone (the door opening knocks them flat: `fx`).
        void ClearCellGuards(int slot, bool fx)
        {
            scratchIds.Clear();
            foreach (var e in Enemies.Values)
                if (CellSlotAt(e.Pos, CellZone) == slot)
                    scratchIds.Add(e.Id);
            foreach (int id in scratchIds)
            {
                if (fx)
                    BroadcastFx(FxKind.Spark, Enemies[id].Pos, new Rgba(0.6f, 0.75f, 1f), 26f, 0.35f, Sfx.None);
                Enemies.Remove(id);
            }
            scratchIds.Clear();
        }

        /// Straight back to where they were netted, the cell emptied (downed inside, the fight over, the player gone).
        void FreeFromCell(HoldingCell cell, Player p, string why)
        {
            ClearCellGuards(cell.Slot, false);
            HoldingCells.Remove(cell);
            if (p != null && InCellZone(p.Pos))
            {
                V2 back = PushOutOfProps(ClampToArena(cell.At, p.Def.BodyR), p.Def.BodyR);
                MovePlayer(p, back);
                p.Disp = back;
                if (!p.Downed)
                    p.InvulnT = Math.Max(p.InvulnT, CellShield);
                BroadcastFx(FxKind.Ring, back, p.Def.Color, 70f, 0.5f, Sfx.Blink);
            }
            DoLog("Holding Cell " + cell.Slot + ": player " + cell.PlayerId + " sent back (" + why + ")");
        }

        /// Host: the net and the cells hold their player still (WarpT): wrapped in the net, the fade in, the door opening.
        void HoldNetted()
        {
            var w = TheWarden()?.Warden;
            foreach (var p in Players.Values)
            {
                bool hold = false;
                if (w != null && w.NetId == p.Id && w.NetT >= NetFlight && !p.Downed)
                    hold = true;
                var cell = CellOf(p);
                if (cell != null && !p.Downed && (cell.Age < CellFadeIn || cell.Out))
                    hold = true;
                if (hold)
                {
                    p.WarpT = Math.Max(p.WarpT, 0.25f);
                    cellHolds.Add(p.Id);
                }
                else if (cellHolds.Remove(p.Id))
                    p.WarpT = 0f;
            }
        }

        /// Host, as an ability is cast (Abilities.cs): inside a cell it takes CellSkillCut off the clock.
        void CellSkillUsed(Player p)
        {
            var cell = CellOf(p);
            if (cell == null || cell.Out || !InCellZone(p.Pos))
                return;
            cell.Left = Math.Max(0f, cell.Left - CellSkillCut);
            cell.Skills++;
        }

        /// Host, in PlayerShotHits: a shot on a Holding Cell's padlock (out in the arena, where its player was netted).
        bool CellLockHit(Bullet b)
        {
            if (HoldingCells.Count == 0)
                return false;
            foreach (var cell in HoldingCells)
                if (!cell.Out && cell.PlayerId != b.Owner && b.Pos.CloserThan(cell.At, CellLockR + b.Radius))
                {
                    TakeCellLockHit(cell);
                    return true;
                }
            return false;
        }

        /// Host: a blast, a swing or a beam (GateLockStrike's callers) on a padlock: one hit each.
        void CellLockStrike(Func<V2, bool> reaches)
        {
            foreach (var cell in HoldingCells)
                if (!cell.Out && reaches(cell.At))
                    TakeCellLockHit(cell);
        }

        void TakeCellLockHit(HoldingCell cell)
        {
            if (hostClock - cell.LockAt < CellLockGap)
                return;  // (the shot still stops on it)
            cell.LockAt = hostClock;
            cell.Left = Math.Max(0f, cell.Left - CellLockCut);
            cell.LockHits++;
            BroadcastFx(FxKind.Spark, cell.At, new Rgba(1f, 0.82f, 0.35f), 16f, 0.15f, Sfx.Hit);
        }

        /// A bot's game: locked in a cell it backs away from the guards, shoots the nearest and casts everything it has
        /// (each cast is a second off); outside, with a teammate locked up, it walks into range of their padlock and shoots it.
        void CellBot(Player me, ref V2 move, ref V2 aimAt, ref bool firing, bool[] held)
        {
            if (me.Downed)
                return;
            int slot = CellSlotAt(me.Pos, CellZone);
            if (slot >= 0)
            {
                V2 c = CellCentre(slot), push = (c - me.Pos) / CellHalf * 0.5f;
                Enemy near = null;
                float best = float.MaxValue;
                foreach (var e in Enemies.Values)
                {
                    if (CellSlotAt(e.Pos, CellZone) != slot || IsSpawning(e))
                        continue;
                    V2 d = me.Pos - e.Pos;
                    float dist = d.Length;
                    if (dist < 230f && dist > 0.01f)
                        push += d / dist * ((230f - dist) / 230f);
                    if (dist < best)
                    {
                        best = dist;
                        near = e;
                    }
                }
                move = push.Length > 0.15f ? push.LimitLength(1f) : V2.FromAngle(botT * 1.1f) * 0.5f;
                if (near != null)
                {
                    aimAt = near.Pos;
                    firing = true;
                }
                for (int i = 0; i < held.Length; i++)
                    held[i] = MathX.Fmod(botT + i * 0.29f, 0.9f) < 0.12f;
                me.Aim = (aimAt - me.Pos).Angle;
                return;
            }
            foreach (var cell in HoldingCells)
                if (!cell.Out && cell.PlayerId != me.Id)
                {
                    aimAt = cell.At;  // a teammate's padlock: every hit is a quarter second off
                    firing = true;
                    if (me.Pos.FartherThan(cell.At, Math.Max(40f, Math.Min(360f, WeaponRange(me) * 0.75f))))
                        move = (cell.At - me.Pos).Normalized();  // into range first
                    me.Aim = (aimAt - me.Pos).Angle;
                    return;
                }
        }

        /// Every game: a cell's clock runs on between snapshots (the host's runs in TickHoldingCells).
        void UpdateHoldingCellView(float dt)
        {
            if (IsHost)
                return;
            foreach (var c in HoldingCells)
            {
                c.Age += dt;
                if (c.Out)
                    c.OutT += dt;
                else
                    c.Left = Math.Max(0f, c.Left - dt);
            }
        }

        void SyncHoldingCells(NetSync s)
        {
            int n = HoldingCells.Count;
            s.Byte(ref n);
            if (s.Reading)
            {
                while (HoldingCells.Count > n)
                    HoldingCells.RemoveAt(HoldingCells.Count - 1);
                while (HoldingCells.Count < n)
                    HoldingCells.Add(new HoldingCell());
            }
            foreach (var c in HoldingCells)
            {
                s.Int(ref c.PlayerId);
                s.Byte(ref c.Slot);
                s.Float(ref c.Left);
                s.Vec(ref c.At);
                s.Float(ref c.Age);
                s.Short(ref c.LockHits);
                s.Byte(ref c.Skills);
                s.Bool(ref c.Out);
                s.Float(ref c.OutT);
            }
        }
    }
}
