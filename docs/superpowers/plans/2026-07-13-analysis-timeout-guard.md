# Analysis Timeout Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Automatically terminate algorithm tasks that remain too long in startup or processing stages while preserving user cancellation behavior.

**Architecture:** Add a small clock-driven watchdog with no process dependencies, then connect it to stdout status handling in `GaitAnalysisService`. Convert watchdog and total-timeout events into failed analysis results, while external cancellation continues to throw `OperationCanceledException`.

**Tech Stack:** C# 13, .NET 10, xUnit, WPF localization resources.

## Global Constraints

- Total timeout effective range is 1 to 10 minutes.
- Startup timeout is 60 seconds.
- Pose-estimation stage timeout is 5 minutes.
- Other processing-stage timeout is 2 minutes.
- No new person-detection dependency is introduced.

---

### Task 1: Timeout policy and watchdog

**Files:**
- Create: `BTFX/Services/Implementations/AlgorithmProgressWatchdog.cs`
- Create: `BTFX.Tests/AlgorithmProgressWatchdogTests.cs`

- [ ] Write tests for total-timeout clamping and each watchdog state.
- [ ] Run the focused tests and verify they fail because the watchdog does not exist.
- [ ] Implement the minimal thread-safe watchdog.
- [ ] Run the focused tests and verify they pass.

### Task 2: Algorithm process integration

**Files:**
- Modify: `BTFX/Services/Implementations/GaitAnalysisService.cs`
- Modify: `BTFX/Resources/Localization/Strings.zh.xaml`
- Modify: `BTFX/Resources/Localization/Strings.en.xaml`
- Test: `BTFX.Tests/AlgorithmProgressWatchdogTests.cs`

- [ ] Observe each valid stdout status in the watchdog.
- [ ] Monitor the watchdog without blocking stdout readers or the UI thread.
- [ ] Kill the process tree and return a failed result on watchdog or total timeout.
- [ ] Keep external cancellation mapped to the existing pending-state workflow.
- [ ] Add concise bilingual timeout messages.
- [ ] Run `dotnet test BTFX.Tests/BTFX.Tests.csproj -c Release --no-restore` and verify all tests pass.
