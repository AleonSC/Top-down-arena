using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Blood Debt (Core/BloodDebt.cs), look A "Ledger" of Docs/Mockups/blood-debt.html: the debt is
    /// a hatched ghost segment on the health bar, each kill throws gold-rimmed blood coins into it, a gold ring round you
    /// counts the window down and an "OWED" tally sits by the bar. Paid in full, a gold "PAID IN FULL" stamp; forfeited,
    /// the ghost segment breaks into falling shards under a red "FORFEIT".
    public partial class ArenaApp
    {
        static readonly Rgba DebtGold = new Rgba(1f, 0.8f, 0.35f);

        /// World, after the player's health bar (WorldPlayers.cs): the ghost segment of what's still owed.
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
            FillRect(x0, bar.Y, x1 - x0, h, BloodDark.WithAlpha(0.85f));
            for (float x = x0 + 1f; x < x1 - 0.5f; x += 3f)  // the hatching
                FillRect(x, bar.Y, Mathf.Min(1.2f, x1 - x), h, BloodRed.WithAlpha(0.55f + 0.2f * Mathf.Sin(t * 6f)));
            FillRect(x1 - 1f, bar.Y - 1f, 1.5f, h + 2f, DebtGold);  // where you'd be, paid back
        }

        /// World, over the model (BloodMageLook.cs): the window counting down while you owe; a soft gold glow once paid.
        void DrawDebtRing(Player p, float r, float t, float a)
        {
            if (p.DebtT <= 0f)
                return;
            if (p.Debt > 0f)
            {
                float left = Mathf.Clamp01(p.DebtT / Game.DebtTime);
                Arc(p.Disp, r + 9f, 2f, BloodDark.WithAlpha(0.7f * a), 28);
                Arc(p.Disp, r + 9f, 2f, DebtGold.WithAlpha((left < 0.25f ? 0.6f + 0.4f * Mathf.Sin(t * 18f) : 0.9f) * a), 28, -MathX.Pi / 2f, MathX.Tau * left);
            }
            else
                Arc(p.Disp, r + 7f + Mathf.Sin(t * 6f), 2.5f, DebtGold.WithAlpha((0.35f + 0.15f * Mathf.Sin(t * 6f)) * a), 24);
        }

        /// A kill's repayment (FxKind.DebtCoin): blood coins with gold rims arcing from the kill into the debtor's bar.
        void DrawDebtCoinFx(Effect fx, float f)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                return;
            V2 to = p.Disp + new V2(0f, -p.Def.BodyR - 10f);
            int n = Mathf.Clamp(Mathf.RoundToInt(fx.Radius / 2f), 2, 6);
            for (int k = 0; k < n; k++)
            {
                float g = Mathf.Clamp01((f - k * 0.07f) / 0.75f);
                if (g <= 0f || g >= 1f)
                    continue;
                float e = g * g * (3f - 2f * g);
                V2 a = fx.Pos + new V2((k - n * 0.5f) * 5f, 0f);
                V2 mid = (a + to) * 0.5f + new V2(0f, -50f - 12f * k);
                V2 pos = a * ((1f - e) * (1f - e)) + mid * (2f * (1f - e) * e) + to * (e * e);
                float rr = 3.4f * (1f - 0.3f * e);
                Circle(pos, rr, BloodRed.WithAlpha(1f - 0.3f * e), 10);
                Arc(pos, rr, 1.1f, DebtGold.WithAlpha(1f - 0.3f * e), 10);
            }
        }

        /// A debt settled (FxKind.DebtSettle): paid, a gold burst round you; forfeited, shards of the ghost segment fall.
        void DrawDebtSettleFx(Effect fx, float f)
        {
            Player p;
            if (!game.Players.TryGetValue((int)fx.Pos2.X, out p))
                return;
            float r = p.Def.BodyR;
            if (fx.Pos2.Y > 0.5f)
            {
                float g = 1f - (1f - f) * (1f - f);
                Arc(p.Disp, r + 10f + 40f * g, 3f * (1f - f), DebtGold.WithAlpha(1f - f), 32);
                for (int k = 0; k < 10; k++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * k / 10f + 0.3f);
                    Circle(p.Disp + d * (r + 8f + 46f * g), 2f * (1f - f), DebtGold.WithAlpha(1f - f), 6);
                }
                return;
            }
            V2 bar = p.Disp + new V2(-20f, -r - 12f);
            float lost = Mathf.Clamp01(fx.Radius / Mathf.Max(1f, p.MaxHp)), from = Mathf.Clamp01(p.Hp / p.MaxHp);
            int n = Mathf.Clamp(Mathf.RoundToInt(lost * 14f), 3, 9);
            for (int k = 0; k < n; k++)
            {
                float x = bar.X + 40f * (from + lost * (k + 0.5f) / n);
                V2 at = new V2(x + (k - n / 2f) * 6f * f, bar.Y + 2f + 70f * f * f + 10f * f * (k % 3));
                V2 u = V2.FromAngle(k * 1.9f + f * (3f + k)), s = u.Orthogonal();
                SetColor((k % 2 == 0 ? BloodRed : BloodDark).WithAlpha(1f - f));
                Quad(at - u * 2.5f - s * 1.5f, at + u * 2.5f - s * 1.5f, at + u * 2.5f + s * 1.5f, at - u * 2.5f + s * 1.5f);
            }
        }

        /// On the HUD, over the arena: the "OWED" tally by each debtor's bar, and the stamps when a debt settles.
        void DrawDebtLedgers()
        {
            if (game == null)
                return;
            foreach (var p in game.Players.Values)
            {
                if (p.Debt <= 0.5f || p.Downed || LaunchHides(p))
                    continue;
                V2 at = ToUi(p.Disp + new V2(22f, -p.Def.BodyR - 10f));
                Ui.Shadowed(new Rect(at.X + 2f, at.Y - 8f, 80f, 16f), "OWED " + Mathf.CeilToInt(p.Debt), 10, ToColor(DebtGold), TextAnchor.MiddleLeft);
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.DebtSettle)
                    continue;
                Player p;
                if (!game.Players.TryGetValue((int)fx.Pos2.X, out p) || LaunchHides(p))
                    continue;
                float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
                float pop = Mathf.Min(1f, fx.T * 8f), fade = f < 0.7f ? 1f : 1f - (f - 0.7f) / 0.3f;
                bool paid = fx.Pos2.Y > 0.5f;
                V2 at = ToUi(p.Disp);
                at.Y -= p.Def.BodyR * WorldToUi + 30f + 8f * f;
                Color c = paid ? ToColor(DebtGold) : ToColor(BloodRed.Mix(Rgba.White, 0.25f));
                c.a = fade;
                int size = Mathf.RoundToInt(15f * (1.6f - 0.6f * pop));  // slams down onto the bar
                Ui.Shadowed(new Rect(at.X - 100f, at.Y - 12f, 200f, 24f), paid ? "PAID IN FULL" : "FORFEIT -" + Mathf.CeilToInt(fx.Radius), size, c);
            }
        }
    }
}
