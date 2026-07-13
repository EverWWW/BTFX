namespace BTFX.Helpers;

internal static class GaitLengthUnitConverter
{
    internal static double ToMeters(double centimeters) => centimeters / 100d;

    internal static double? ToMeters(double? centimeters) =>
        centimeters.HasValue ? ToMeters(centimeters.Value) : null;
}
