using BTFX.Services.Implementations;
using Xunit;

namespace BTFX.Tests;

public sealed class CameraDialogLifetimeTests
{
    [Fact]
    public void Close_PermanentlyPreventsPreviewWork()
    {
        using var lifetime = new CameraDialogLifetime();

        Assert.True(lifetime.CanStartPreview);

        lifetime.Close();

        Assert.True(lifetime.IsClosed);
        Assert.False(lifetime.CanStartPreview);
        Assert.True(lifetime.Token.IsCancellationRequested);
    }

    [Fact]
    public void Close_IsIdempotent()
    {
        using var lifetime = new CameraDialogLifetime();

        lifetime.Close();
        lifetime.Close();

        Assert.True(lifetime.IsClosed);
    }

    [Fact]
    public void Dispose_LeavesCanceledTokenReadableForInFlightWork()
    {
        var lifetime = new CameraDialogLifetime();

        lifetime.Dispose();

        Assert.True(lifetime.Token.IsCancellationRequested);
    }
}
