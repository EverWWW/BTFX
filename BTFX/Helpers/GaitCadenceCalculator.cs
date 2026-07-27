namespace BTFX.Helpers;

/// <summary>
/// 统一按完整步态周期计算步频。一个完整周期包含两步。
/// </summary>
public static class GaitCadenceCalculator
{
    public static double? CalculateFromFullCycle(double? cycleDurationSeconds)
    {
        if (cycleDurationSeconds is not > 0
            || double.IsNaN(cycleDurationSeconds.Value)
            || double.IsInfinity(cycleDurationSeconds.Value))
        {
            return null;
        }

        return 120d / cycleDurationSeconds.Value;
    }

    public static double? PreferCycleDerived(double? cycleDurationSeconds, double? fallbackCadence)
        => CalculateFromFullCycle(cycleDurationSeconds) ?? fallbackCadence;
}
