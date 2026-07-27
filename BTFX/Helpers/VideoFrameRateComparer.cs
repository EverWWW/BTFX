namespace BTFX.Helpers;

internal static class VideoFrameRateComparer
{
    private const double NominalFrameRateTolerance = 0.1;

    internal static bool AreEquivalent(double firstFrameRate, double secondFrameRate)
    {
        if (firstFrameRate <= 0 || secondFrameRate <= 0)
        {
            return false;
        }

        return Math.Abs(firstFrameRate - secondFrameRate) <= NominalFrameRateTolerance;
    }
}
