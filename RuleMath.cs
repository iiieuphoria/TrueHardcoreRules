namespace TrueHardcoreRules;

/// <summary>Pure math, no SPT types, so tests/RuleMathCheck can link it.</summary>
public static class RuleMath
{
    /// <summary>Scale a positive count, never below 1.</summary>
    public static double Scale(double value, double mult) => Math.Max(1, Math.Round(value * mult));

    /// <summary>
    /// BuyPriceCoefficient is the % a trader knocks off when buying from you (Fence 60 = pays 40%).
    /// Returns the coef that makes the trader pay payoutMult times what it paid before.
    /// </summary>
    public static double PayoutCoef(double coef, double payoutMult) => Math.Clamp(100 - (100 - coef) * payoutMult, 0, 100);

    /// <summary>Does an item at (x,y) of size w*h (swapped when vertical) still fit inside a gridW*gridH grid.</summary>
    public static bool Fits(int x, int y, int w, int h, bool vertical, int gridW, int gridH)
    {
        if (vertical) (w, h) = (h, w);
        return x >= 0 && y >= 0 && x + w <= gridW && y + h <= gridH;
    }
}
