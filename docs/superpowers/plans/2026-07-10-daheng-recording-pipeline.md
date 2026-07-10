# DAHENG Recording Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the DAHENG countdown end at the real recording start, retain low-rate live preview while recording, and bound/reuse frame memory without changing YUNXI behavior.

**Architecture:** The ViewModel supplies one UTC scheduled start time to the DAHENG recording service immediately after the button click. The service prepares devices and FFmpeg during the countdown, starts acquisition before the scheduled instant, displays throttled preview frames, and only queues recording frames at the shared start time. Recording frames use pooled leases and a bounded packet channel; one packet can represent repeated timeline frames.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, GxIAPINET, FFmpeg, `System.Threading.Channels`, `System.Buffers`, xUnit.

## Global Constraints

- Keep YUNXI recording and its no-preview-during-recording behavior unchanged.
- DAHENG saved video remains 30/60/90 fps and preview is approximately 10 fps.
- Preserve current DAHENG side/front shared scheduled start strategy.
- Do not change the capture result contract used by the measurement page.
- Preserve all unrelated dirty working-tree changes and do not create a mixed commit.

---

### Task 1: Add testable scheduling and pooled-frame primitives

**Files:**
- Create: `BTFX.Tests/BTFX.Tests.csproj`
- Create: `BTFX.Tests/DahengRecordingPrimitivesTests.cs`
- Create: `BTFX/Services/Implementations/DahengRecordingPrimitives.cs`
- Modify: `BTFX.slnx`
- Modify: `BTFX/Properties/AssemblyInfo.cs`

**Interfaces:**
- Produces: `DahengRecordingSchedule.ResolveStartAt(DateTimeOffset requested, DateTimeOffset ready, TimeSpan margin)`.
- Produces: `PooledFrameLease`, which owns one rented byte array and returns it exactly once after all references are released.
- Produces: `DahengFramePacket(PooledFrameLease Lease, int Length, int RepeatCount)`.

- [ ] **Step 1: Write failing scheduling tests**

```csharp
[Fact]
public void ResolveStartAt_UsesRequestedTimeWhenPreparationFinishesEarly()
{
    var requested = DateTimeOffset.Parse("2026-07-10T10:00:05Z");
    var ready = requested.AddSeconds(-2);
    Assert.Equal(requested, DahengRecordingSchedule.ResolveStartAt(requested, ready, TimeSpan.FromMilliseconds(250)));
}

[Fact]
public void ResolveStartAt_AddsMarginWhenPreparationFinishesLate()
{
    var requested = DateTimeOffset.Parse("2026-07-10T10:00:05Z");
    var ready = requested.AddMilliseconds(100);
    Assert.Equal(ready.AddMilliseconds(250), DahengRecordingSchedule.ResolveStartAt(requested, ready, TimeSpan.FromMilliseconds(250)));
}
```

- [ ] **Step 2: Run tests and verify RED**

Run: `dotnet test BTFX.Tests/BTFX.Tests.csproj --filter DahengRecordingPrimitivesTests`

Expected: compilation fails because `DahengRecordingSchedule` does not exist.

- [ ] **Step 3: Implement schedule resolution**

```csharp
internal static class DahengRecordingSchedule
{
    public static DateTimeOffset ResolveStartAt(DateTimeOffset requested, DateTimeOffset ready, TimeSpan margin)
    {
        var earliestSafeStart = ready + margin;
        return requested >= earliestSafeStart ? requested : earliestSafeStart;
    }
}
```

- [ ] **Step 4: Write lease lifetime tests and verify RED**

Use a small custom `ArrayPool<byte>` in the test and assert that two `AddReference()` calls followed by three `Release()` calls return the array exactly once.

- [ ] **Step 5: Implement the pooled lease and packet**

```csharp
internal sealed class PooledFrameLease
{
    private readonly ArrayPool<byte> _pool;
    private int _referenceCount = 1;
    private int _returned;

    public PooledFrameLease(ArrayPool<byte> pool, int minimumLength)
    {
        _pool = pool;
        Buffer = pool.Rent(minimumLength);
    }

    public byte[] Buffer { get; }

    public void AddReference() => Interlocked.Increment(ref _referenceCount);

    public void Release()
    {
        if (Interlocked.Decrement(ref _referenceCount) == 0
            && Interlocked.Exchange(ref _returned, 1) == 0)
        {
            _pool.Return(Buffer);
        }
    }
}

internal sealed record DahengFramePacket(PooledFrameLease Lease, int Length, int RepeatCount);
```

- [ ] **Step 6: Run tests and verify GREEN**

Run: `dotnet test BTFX.Tests/BTFX.Tests.csproj --filter DahengRecordingPrimitivesTests`

Expected: all primitive tests pass.

### Task 2: Prepare DAHENG during countdown and keep recording preview visible

**Files:**
- Modify: `BTFX/Models/Camera/CameraRecordingOptions.cs`
- Modify: `BTFX/ViewModels/CameraCaptureDialogViewModel.cs`
- Modify: `BTFX/Services/Implementations/DahengCameraRecordingService.cs`
- Modify: `BTFX/Views/Dialogs/CameraCaptureDialog.xaml`

**Interfaces:**
- Consumes: `DahengRecordingSchedule.ResolveStartAt(...)`.
- Produces: `CameraRecordingOptions.ScheduledStartAtUtc`.
- Produces progress messages `STAGE:PREPARING`, `STAGE:COUNTDOWN:<round-trip UTC>`, and `STAGE:RECORD_START`.

- [ ] **Step 1: Add scheduled-start option**

```csharp
public DateTimeOffset? ScheduledStartAtUtc { get; set; }
```

- [ ] **Step 2: Start the DAHENG service before awaiting the countdown**

In `StartRecordingAsync`, calculate `requestedStartAt = DateTimeOffset.UtcNow.AddSeconds(RecordingStartDelaySeconds)`. Keep the existing awaited delay only for YUNXI. For DAHENG, pass `requestedStartAt` in the options and call `RecordAsync` immediately.

- [ ] **Step 3: Resolve the real start after device preparation**

After all slots call `OpenAndPrepare`, calculate:

```csharp
var startAt = DahengRecordingSchedule.ResolveStartAt(
    options.ScheduledStartAtUtc ?? DateTimeOffset.UtcNow,
    DateTimeOffset.UtcNow,
    TimeSpan.FromMilliseconds(250));
```

Report the resolved start, call `BeginRecording(startAt, endAt)` for all slots, then start both acquisitions.

- [ ] **Step 4: Publish preview before the scheduled recording instant**

In the frame callback, convert and publish the throttled preview while acquisition is active. Only timeline queueing remains guarded by `now >= _recordingStartAt`.

- [ ] **Step 5: Show image during DAHENG recording**

Add a ViewModel property that is true for normal preview and for DAHENG recording before finalization. Bind both camera `Image.Visibility` values to that property. Keep the recording text and progress as overlays.

- [ ] **Step 6: Verify the project builds**

Run: `dotnet build BTFX/BTFX.csproj`

Expected: build succeeds with 0 errors.

### Task 3: Replace unbounded per-frame allocation with bounded pooled packets

**Files:**
- Modify: `BTFX/Services/Implementations/DahengCameraRecordingService.cs`
- Test: `BTFX.Tests/DahengRecordingPrimitivesTests.cs`

**Interfaces:**
- Consumes: `PooledFrameLease` and `DahengFramePacket`.
- Produces: bounded `Channel<DahengFramePacket>` and queue peak diagnostics.

- [ ] **Step 1: Change the channel to a bounded packet channel**

Create a bounded channel with a small fixed capacity and single reader/single writer. Track current depth and peak depth with `Interlocked`.

- [ ] **Step 2: Convert SDK frames into rented buffers**

Rent a `PooledFrameLease`, copy the native BGR bytes into `lease.Buffer`, and release the callback's reference in `finally`.

- [ ] **Step 3: Queue one packet per captured frame interval**

Calculate `repeatCount = desiredFrameIndex - _nextFrameIndex + 1`. Add one lease reference and enqueue one `DahengFramePacket`; only advance `_nextFrameIndex` after successful enqueue. If the queue is full, leave the timeline index unchanged so the next successful frame covers the missed interval.

- [ ] **Step 4: Write repeats and return memory**

```csharp
try
{
    for (var index = 0; index < packet.RepeatCount; index++)
    {
        await process.StandardInput.BaseStream.WriteAsync(packet.Lease.Buffer.AsMemory(0, packet.Length));
        Interlocked.Increment(ref _writtenFrameCount);
    }
}
finally
{
    packet.Lease.Release();
}
```

- [ ] **Step 5: Flush remaining timeline frames before channel completion**

Hold one reference to the latest successful frame. During stop, asynchronously enqueue one final packet for the remaining repeat count, then complete the channel. Release the retained latest-frame reference during cleanup.

- [ ] **Step 6: Add queue and timing diagnostics**

Log preparation duration, actual first recording frame UTC, captured/queued/written/repeated counts, queue peak, and output path for each camera.

- [ ] **Step 7: Run tests and build**

Run: `dotnet test BTFX.Tests/BTFX.Tests.csproj`

Expected: all tests pass.

Run: `dotnet build BTFX/BTFX.csproj`

Expected: build succeeds with 0 errors and 0 warnings.

### Task 4: Final review and hardware acceptance checklist

**Files:**
- Review: `BTFX/Models/Camera/CameraRecordingOptions.cs`
- Review: `BTFX/Services/Implementations/DahengCameraRecordingService.cs`
- Review: `BTFX/ViewModels/CameraCaptureDialogViewModel.cs`
- Review: `BTFX/Views/Dialogs/CameraCaptureDialog.xaml`

- [ ] **Step 1: Inspect the scoped diff**

Run: `git diff -- BTFX/Models/Camera/CameraRecordingOptions.cs BTFX/Services/Implementations/DahengCameraRecordingService.cs BTFX/ViewModels/CameraCaptureDialogViewModel.cs BTFX/Views/Dialogs/CameraCaptureDialog.xaml BTFX.Tests docs/superpowers`

Expected: every changed line maps to scheduling, recording preview, pooling, bounded queue, diagnostics, tests, or documentation.

- [ ] **Step 2: Run complete verification**

Run: `dotnet test BTFX.Tests/BTFX.Tests.csproj && dotnet build BTFX/BTFX.csproj`

Expected: tests and build succeed.

- [ ] **Step 3: Perform hardware acceptance**

With two DAHENG cameras, run three consecutive 10-second 2048x1536 recordings at 90 fps. Verify countdown preview, recording preview, synchronized motion, stable memory between runs, exact frame counts, and ffprobe duration/fps.

- [ ] **Step 4: Do not commit mixed workspace changes**

The current working tree contains prior user work in the same files. Leave changes uncommitted and report the exact files and verification evidence to the user.

