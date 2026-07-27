using System.Reflection;
using BTFX;
using Xunit;

namespace BTFX.Tests;

public sealed class VideoFrameRateComparerTests
{
    [Theory]
    [InlineData(30.00, 30.02, true)]
    [InlineData(29.97, 30.00, true)]
    [InlineData(59.94, 60.00, true)]
    [InlineData(30.00, 30.11, false)]
    [InlineData(30.00, 45.00, false)]
    [InlineData(0.00, 30.00, false)]
    public void AreEquivalent_UsesNominalFrameRateTolerance(
        double firstFrameRate,
        double secondFrameRate,
        bool expected)
    {
        var type = typeof(App).Assembly.GetType("BTFX.Helpers.VideoFrameRateComparer");
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "AreEquivalent",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var actual = Assert.IsType<bool>(
            method!.Invoke(null, [firstFrameRate, secondFrameRate]));
        Assert.Equal(expected, actual);
    }
}

public sealed class AlgorithmProcessWindowSuppressorTests
{
    [Fact]
    public void CollectProcessTree_IncludesOnlyRootAndItsDescendants()
    {
        var type = typeof(App).Assembly.GetType("BTFX.Helpers.AlgorithmProcessWindowSuppressor");
        Assert.NotNull(type);

        var method = type!.GetMethod(
            "CollectProcessTree",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        IReadOnlyDictionary<int, int> parentByProcessId = new Dictionary<int, int>
        {
            [100] = 1,
            [101] = 100,
            [102] = 101,
            [200] = 1,
            [201] = 200
        };

        var result = Assert.IsAssignableFrom<IEnumerable<int>>(
            method!.Invoke(null, [100, parentByProcessId]));

        Assert.Equal([100, 101, 102], result.OrderBy(processId => processId));
    }
}
