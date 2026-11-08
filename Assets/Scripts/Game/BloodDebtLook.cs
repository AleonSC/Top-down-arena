using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Blood Debt (Core/BloodDebt.cs), look A "Ledger" of Docs/Mockups/blood-debt.html, ported from
    /// the mockup. The cast bursts blood off you with "−N BORROWED". While you owe, a red ring counts the 8 s down and the
    /// debt is a hatched ghost segment on your health bar. Your own debt also gets the ledger card beside you: a big health
    /// bar with the hatched ghost, and OWED, PAID and LEFT (LOST after a forfeit). Each kill spits gold-rimmed blood coins
    /// that arc into the ghost (on your card; on everyone else's bar over their head) with "+N REPAID". Paying in full
    /// slams a tilted gold "PAID IN FULL" stamp onto the bar; a forfeit cracks the ghost into falling shards under a red
    /// "FORFEIT −N" stamp.
    public partial class ArenaApp
    {
        static readonly Rgba DebtGold = new Rgba(0.85f, 0.64f, 0.29f), DebtFresh = new Rgba(1f, 0.29f, 0.34f), DebtGhost = new Rgba(0.49f, 0.05f, 0.13f);
        static readonly Rgba DebtInk = new Rgba(0.95f, 0.9f, 0.89f), DebtMuted = new Rgba(0.7f, 0.56f, 0.54f), DebtRepaid = new Rgba(1f, 0.84f, 0.63f);
        const float LedgerW = 156f, LedgerBarH = 12f;

        // --- World ------------------------------------------------------------------------------------------------

        /// After the player's health bar (WorldPlayers.cs): the ghost segment of what's still owed, hatched and outlined.
        void DrawDebtBar(Player p, float r, float t)
        {
            if (p.Debt <= 0f || p.MaxHp <= 0f)
                return;
            V2 bar = p.Disp + new V2(-20f, -r - 12f);
            const float w = 40f, h = 4f;
            float from = Mathf.Clamp01(p.Hp / p.MaxHp), to = Mathf.Clamp01((p.Hp + p.Debt) / p.MaxHp);
            if (to - from < 0.005f)
                return;
            float x0 = bar.X + w * from, x1 = bar.X + w * to;
            FillRect(x0, bar.Y, x1 - x0, h, DebtGhost.WithAlpha(0.75f));
            for (float x = x0 + 1f; x < x1 - 0.5f; x += 2.5f)  // the hatching
                FillRect(x, bar.Y, Mathf.Min(1f, x1 - x), h, DebtFresh.WithAlpha(0.55f));
            FillRect(x0, bar.Y - 0.6f, x1 - x0, 0.6f, DebtFresh);
            FillRect(x0, bar.Y + h, x1 - x0, 0.6f, DebtFresh);
            FillRect(x1 - 0.6f, bar.Y - 0.6f, 0.6f, h + 1.2f, DebtFresh);
        }

        /// Over the model (WorldPlayers.cs): the window counting down while you owe; a soft gold glow once it's paid.
        void DrawDebtRing(Player p, float r, float t, float a)
        {
            if (p.DebtT <= 0f)
                return;
            if (p.Debt > 0f)
            {
                float left = Mathf.Clamp01(p.DebtT / Game.DebtTime);
                float pulse = left < 0.25f ? 0.55f + 0.45f * Mathf.Sin(t * 18f) : 1f;
                Arc(p.Disp, r + 9f, 2.5f, DebtFresh.WithAlpha(0.7f * pulse * a), 32, -MathX.Pi / 2f, MathX.Tau * left);
            }
            else
                Arc(p.Disp, r + 7f + Mathf.Sin(t * 6f), 2.5f, DebtGold.WithAlpha((0.35f + 0.15f * Mathf.Sin(t * 6f)) * a), 24);
        }

        /// The coins of one repayment: how many there are, and each one's arc (the mockup's numbers, in % of max health).
        static int DebtCoins(Player p, float amount) => Mathf.Clamp(Mathf.RoundToInt(amount / Mathf.Max(1f, p.MaxHp) * 100f / 1.5f), 2, 8);

        static bool DebtCoinAt(Effect fx, int k, V2 from, V2 to, out V2 pos)
        {
            float u = Mathf.Clamp01((fx.T - k * 0.06f) * 1.6f);
            pos = from;
            if (fx.T - k * 0.06f <= 0f || u >= 1f)
                return false;
            float e = 1f - (1f - u) * (1f - u) * (1f - u);
            float arc = -60f - 80f * SprayHash(fx.Pos.X * 0.13f + k * 1.7f);
            pos = from + (to - from) * e + new V2(0f, Mathf.Sin(u * MathX.Pi) * arc);
            return true;
        }

        /// A kill's repayment (FxKind.DebtCoin): coins arcing from the kill into the debtor's bar over their head (your own
        /// coins fly to your ledger card instead, on the HUD).
        void DrawDebtCoinFx(Effect fx, float f)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p) || p.Id == game.MyId)
                return;
            float r = p.Def.BodyR;
            float from = Mathf.Clamp01(p.Hp / Mathf.Max(1f, p.MaxHp)), to = Mathf.Clamp01((p.Hp + p.Debt) / Mathf.Max(1f, p.MaxHp));
            int n = DebtCoins(p, fx.Radius);
            for (int k = 0; k < n; k++)
            {
                V2 target = p.Disp + new V2(-20f + 40f * Mathf.Lerp(from, Mathf.Max(from, to), SprayHash(fx.Pos.Y + k)), -r - 10f), pos;
                if (!DebtCoinAt(fx, k, fx.Pos, target, out pos))
                    continue;
                Circle(pos, 3.2f, DebtGold, 10);
                Circle(pos, 1.9f, new Rgba(0.69f, 0.08f, 0.17f), 8);
            }
        }

        /// A debt's moments (FxKind.DebtSettle): borrowed, a burst of blood off you and a ring; paid, a gold ring; forfeited,
        /// the ghost on the bar over the debtor's head cracks into falling shards (yours crack off the ledger card).
        void DrawDebtSettleFx(Effect fx, float f)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                return;
            float r = p.Def.BodyR, age = fx.T;
            if (fx.Pos2.Y > 1.5f)  // borrowed
            {
                for (int k = 0; k < 26; k++)
                {
                    if (age > 0.9f)
                        break;
                    float ang = SprayHash(fx.Pos.X + k * 3.3f) * MathX.Tau, v = 60f + 140f * SprayHash(fx.Pos.Y + k * 1.9f);
                    float travel = v * (1f - Mathf.Exp(-1.2f * age)) / 1.2f;  // (the mockup's 2% drag a frame)
                    V2 at = fx.Pos + V2.FromAngle(ang) * travel;
                    Circle(at, 2.2f * (1f - age / 0.9f) + 0.6f, (k % 3 != 0 ? DebtFresh : DebtGhost).WithAlpha(1f - age / 0.9f), 6);
                }
                float g = Mathf.Clamp01(age / 0.6f);
                Arc(fx.Pos, 14f + 46f * (1f - (1f - g) * (1f - g)), 2.5f * (1f - g), DebtFresh.WithAlpha(1f - g), 32);
                return;
            }
            if (fx.Pos2.Y > 0.5f)  // paid
            {
                float g = Mathf.Clamp01(age / 0.8f), e = 1f - (1f - g) * (1f - g);
                Arc(p.Disp, r + 10f + 40f * e, 3f * (1f - g), DebtGold.WithAlpha(1f - g), 32);
                for (int k = 0; k < 10; k++)
                    Circle(p.Disp + V2.FromAngle(MathX.Tau * k / 10f + 0.3f) * (r + 8f + 46f * e), 2f * (1f - g), DebtGold.WithAlpha(1f - g), 6);
                return;
            }
            if (p.Id == game.MyId)
                return;  // (your shards fall off the ledger card)
            V2 bar = p.Disp + new V2(-20f, -r - 12f);
            float lost = Mathf.Clamp01(fx.Radius / Mathf.Max(1f, p.MaxHp)), from = Mathf.Clamp01(p.Hp / p.MaxHp);
            int n = Mathf.Clamp(Mathf.RoundToInt(lost * 40f / 3f), 3, 10);
            for (int k = 0; k < n; k++)
            {
                float life = 1f - age * 0.7f / 1.4f;
                if (life <= 0f)
                    break;
                float vx = (SprayHash(k * 2.1f + fx.Pos.X) - 0.5f) * 90f, vy = -60f - 90f * SprayHash(k * 3.7f + fx.Pos.Y);
                V2 at = new V2(bar.X + 40f * (from + lost * (k + 0.5f) / n) + vx * age * 0.4f, bar.Y + 2f + vy * age * 0.4f + 130f * age * age * 0.16f);
                V2 u = V2.FromAngle(k * 1.9f + age * (3f + k)), s = u.Orthogonal();
                SetColor((k % 2 == 0 ? DebtGhost : new Rgba(0.65f, 0.1f, 0.19f)).WithAlpha(Mathf.Clamp01(life)));
                Quad(at - u * 2f - s * 1.2f, at + u * 2f - s * 1.2f, at + u * 2f + s * 1.2f, at - u * 2f + s * 1.2f);
            }
        }

        // --- HUD ------------------------------------------------------------------------------------------------

        static Color UiCol(Rgba c, float a = 1f) => new Color(c.R, c.G, c.B, c.A * a);

        /// On the HUD, over the arena: the pops ("−N BORROWED", "+N REPAID"), for everyone's debt.
        void DrawDebtLedgers()
        {
            if (game == null)
                return;
            foreach (var fx in game.Effects)  // the pops, for everyone's debt
            {
                if (fx.Kind == FxKind.DebtSettle && fx.Pos2.Y > 1.5f && fx.T < 1.4f)
                    RisingPop(fx.Pos + new V2(0f, -30f), fx.T, "-" + Mathf.RoundToInt(fx.Radius) + " BORROWED", DebtFresh, 15);
                else if (fx.Kind == FxKind.DebtCoin && fx.T < 1.4f)
                    RisingPop(fx.Pos + new V2(0f, -18f), fx.T, "+" + (Mathf.Round(fx.Radius * 10f) / 10f) + " REPAID", DebtRepaid, 12);
            }
        }

        /// On the HUD, over the enemies' tags: your ledger card while you owe, and while a settle's stamp plays.
        void DrawMyLedger()
        {
            if (game == null)
                return;
            var me = game.Me;
            if (me == null || LaunchHides(me))
                return;
            Effect settle = null;
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.DebtSettle && (int)fx.Pos2.X == me.Id && fx.Pos2.Y < 1.5f)
                    settle = fx;
            bool owing = me.DebtT > 0f && me.Debt > 0f;
            if (!owing && settle == null && !(me.DebtT > 0f && me.DebtOwed > 0f))
                return;
            DrawLedgerCard(me, settle);
        }

        /// Your ledger card, beside you: the bar with the hatched ghost, the three lines, the coins flying in, the stamps
        /// and a forfeit's shards.
        void DrawLedgerCard(Player me, Effect settle)
        {
            V2 at = ToUi(me.Disp);
            float r = me.Def.BodyR * UiWorldScale;
            bool left = at.X + r + 30f + LedgerW > W - 20f;
            float x = left ? at.X - r - 30f - LedgerW : at.X + r + 30f, y = at.Y - 92f;
            if (y < 70f)
                y = at.Y + r + 20f;
            bool paid = settle != null && settle.Pos2.Y > 0.5f, forfeit = settle != null && !paid;
            float lost = forfeit ? settle.Radius : 0f;
            float owed = me.DebtOwed, debt = Mathf.Max(0f, me.Debt), paidSoFar = Mathf.Max(0f, owed - debt - lost);
            float maxHp = Mathf.Max(1f, me.MaxHp);
            float fade = settle != null ? Mathf.Clamp01((settle.Dur - settle.T) / 0.5f) : 1f;
            if (settle == null && !(me.Debt > 0f))
                fade = Mathf.Clamp01(me.DebtT / 0.5f);  // the paid bonus running out
            // The card
            var card = new Rect(x, y, LedgerW, 84f);
            Ui.Fill(card, new Color(0f, 0f, 0f, 0.45f * fade));
            Ui.Fill(new Rect(card.x, card.y, card.width, 1f), UiCol(DebtFresh, 0.35f * fade));
            // The bar: your health, then the hatched ghost of what's owed
            var bar = new Rect(x + 8f, y + 8f, LedgerW - 16f, LedgerBarH);
            float hpF = Mathf.Clamp01(me.Hp / maxHp), ghostF = Mathf.Clamp01((me.Hp + debt) / maxHp);
            Ui.Fill(bar, new Color(0f, 0f, 0f, 0.6f * fade));
            Ui.Fill(new Rect(bar.x, bar.y, bar.width * hpF, bar.height), new Color(0.3f, 1f, 0.4f, 0.9f * fade));
            float gx = bar.x + bar.width * hpF, gw = bar.width * (ghostF - hpF);
            if (gw > 0.5f)
            {
                Ui.Fill(new Rect(gx, bar.y, gw, bar.height), UiCol(DebtGhost, 0.55f * fade));
                for (float i = -bar.height; i < gw; i += 6f)  // diagonal hatching, clipped to the ghost
                    for (float s = 0f; s < bar.height; s += 1.5f)
                    {
                        float hx = gx + i + s;
                        if (hx >= gx && hx <= gx + gw - 1.5f)
                            Ui.Fill(new Rect(hx, bar.y + bar.height - s - 1.5f, 1.5f, 1.5f), UiCol(DebtFresh, 0.55f * fade));
                    }
                Ui.Fill(new Rect(gx, bar.y, gw, 1f), UiCol(DebtFresh, fade));
                Ui.Fill(new Rect(gx, bar.y + bar.height - 1f, gw, 1f), UiCol(DebtFresh, fade));
                Ui.Fill(new Rect(gx + gw - 1f, bar.y, 1f, bar.height), UiCol(DebtFresh, fade));
            }
            // The lines
            float lx = x + 10f, vx = x + LedgerW - 10f - 60f, ly = bar.y + bar.height + 8f;
            Ui.Label(new Rect(lx, ly, 70f, 16f), "OWED", 11, UiCol(DebtMuted, fade));
            Ui.Label(new Rect(vx, ly, 60f, 16f), Mathf.RoundToInt(owed).ToString(), 12, UiCol(DebtInk, fade), TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(lx, ly + 16f, 70f, 16f), "PAID", 11, UiCol(DebtMuted, fade));
            Ui.Label(new Rect(vx, ly + 16f, 60f, 16f), Mathf.RoundToInt(paidSoFar).ToString(), 12, UiCol(DebtGold, fade), TextAnchor.MiddleRight, true);
            Ui.Fill(new Rect(lx - 2f, ly + 34f, LedgerW - 16f, 1f), new Color(0.23f, 0.1f, 0.12f, fade));
            Ui.Label(new Rect(lx, ly + 37f, 70f, 16f), forfeit ? "LOST" : "LEFT", 11, UiCol(DebtMuted, fade));
            Ui.Label(new Rect(vx, ly + 37f, 60f, 16f), Mathf.RoundToInt(forfeit ? lost : debt).ToString(), 12, UiCol(forfeit ? DebtFresh : DebtInk, fade), TextAnchor.MiddleRight, true);
            // Your coins flying in
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.DebtCoin || (int)fx.Pos2.X != me.Id)
                    continue;
                V2 from = ToUi(fx.Pos);
                int n = DebtCoins(me, fx.Radius);
                for (int k = 0; k < n; k++)
                {
                    V2 target = new V2(gx + 4f + SprayHash(fx.Pos.Y + k) * Mathf.Max(4f, gw - 8f), bar.y + bar.height / 2f), pos;
                    if (!DebtCoinAt(fx, k, from, target, out pos))
                        continue;
                    Ui.Fill(new Rect(pos.X - 4f, pos.Y - 4f, 8f, 8f), UiCol(DebtGold));
                    Ui.Fill(new Rect(pos.X - 2.4f, pos.Y - 2.4f, 4.8f, 4.8f), new Color(0.69f, 0.08f, 0.17f));
                }
            }
            if (settle == null)
                return;
            float age = settle.T;
            if (forfeit)  // shards of the ghost falling off the bar
            {
                float lostF = Mathf.Clamp01(lost / maxHp);
                int n = Mathf.Max(4, Mathf.RoundToInt(bar.width * lostF / 5f));
                for (int k = 0; k < n; k++)
                {
                    float life = 1.4f - age * 0.7f;
                    if (life <= 0f)
                        break;
                    float sx = bar.x + bar.width * (hpF + lostF * SprayHash(k * 1.3f + 0.5f));
                    float vxk = (SprayHash(k * 2.1f) - 0.5f) * 90f, vyk = -60f - 90f * SprayHash(k * 3.7f);
                    float px = sx + vxk * age, py = bar.y + SprayHash(k * 5.3f) * bar.height + vyk * age + 130f * age * age;
                    float sz = 3f + 4f * SprayHash(k * 7.1f);
                    var old = GUI.matrix;
                    GUIUtility.RotateAroundPivot((k * 57f + age * (k % 2 == 0 ? 300f : -260f)) % 360f, new Vector2(px, py));
                    Ui.Fill(new Rect(px - sz / 2f, py - sz / 2f, sz, sz), UiCol(k % 2 == 0 ? DebtGhost : new Rgba(0.65f, 0.1f, 0.19f), Mathf.Clamp01(life / 1.4f)));
                    GUI.matrix = old;
                }
            }
            // The stamp, slammed onto the bar
            float slam = Mathf.Clamp01(age / 0.15f), sc = Mathf.Lerp(2.2f, 1f, slam);
            float sa = slam * Mathf.Clamp01((settle.Dur - age) / 0.5f);
            var c = paid ? UiCol(DebtGold, sa) : UiCol(DebtFresh, sa);
            var center = new Vector2(bar.x + bar.width - 50f, bar.y - 16f);
            var m0 = GUI.matrix;
            GUIUtility.RotateAroundPivot(paid ? -7f : 4.5f, center);
            float sw = (paid ? 112f : 104f + 8f * Mathf.Max(0, Mathf.RoundToInt(lost).ToString().Length - 2)) * sc, sh = 24f * sc;
            var sr = new Rect(center.x - sw / 2f, center.y - sh / 2f, sw, sh);
            Ui.Fill(new Rect(sr.x, sr.y, sr.width, 2f), c);
            Ui.Fill(new Rect(sr.x, sr.yMax - 2f, sr.width, 2f), c);
            Ui.Fill(new Rect(sr.x, sr.y, 2f, sr.height), c);
            Ui.Fill(new Rect(sr.xMax - 2f, sr.y, 2f, sr.height), c);
            Ui.Label(sr, paid ? "PAID IN FULL" : "FORFEIT -" + Mathf.RoundToInt(lost), Mathf.RoundToInt(15f * sc), c, TextAnchor.MiddleCenter, true);
            GUI.matrix = m0;
        }
    }
}
