using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class PreviewGenerationCoordinatorTests
{
    [Fact]
    public void Begin_CancelsAndInvalidatesPreviousGeneration()
    {
        using var coordinator = new PreviewGenerationCoordinator();
        using var first = coordinator.Begin();

        using var second = coordinator.Begin();

        Assert.True(first.Token.IsCancellationRequested);
        Assert.False(coordinator.IsCurrent(first.Version));
        Assert.True(coordinator.IsCurrent(second.Version));
    }

    [Fact]
    public void CancelCurrent_CancelsCurrentGeneration()
    {
        using var coordinator = new PreviewGenerationCoordinator();
        using var generation = coordinator.Begin();

        coordinator.CancelCurrent();

        Assert.True(generation.Token.IsCancellationRequested);
        Assert.False(coordinator.IsCurrent(generation.Version));
    }
}
