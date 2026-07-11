# Automatic Report Creation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create one viewable report record automatically after a measurement analysis result is saved successfully.

**Architecture:** Add a small coordinator around the existing idempotent `IReportService.GetOrCreateDraftReportAsync` operation. Call it from `Step4AnalyzeViewModel.OnAnalysisCompletedAsync` only after the analysis result and completed measurement status are persisted; isolate report creation failures from the successful analysis.

**Tech Stack:** .NET 10, WPF, CommunityToolkit.Mvvm, xUnit, SqlSugar/SQLite.

## Global Constraints

- One report per `MeasurementId`.
- Report creation failure must not change a successful analysis to failed.
- Existing preview, export, print, and report-list filtering behavior remains unchanged.

---

### Task 1: Automatic report coordinator and analysis integration

**Files:**
- Create: `BTFX/Services/Implementations/AnalysisReportCoordinator.cs`
- Modify: `BTFX/App.xaml.cs`
- Modify: `BTFX/ViewModels/Measurement/Step4AnalyzeViewModel.cs`
- Test: `BTFX.Tests/AnalysisReportCoordinatorTests.cs`

**Interfaces:**
- Consumes: `IReportService.GetOrCreateDraftReportAsync(int measurementRecordId, int operatorId)`.
- Produces: `Task<bool> AnalysisReportCoordinator.EnsureReportExistsAsync(int measurementId, int operatorId)`.

- [ ] **Step 1: Write failing coordinator tests**

Test that a successful report result returns `true`, a `null` result returns `false`, and an exception is caught and returns `false`.

- [ ] **Step 2: Run focused tests and verify failure**

Run: `dotnet test BTFX.Tests/BTFX.Tests.csproj --no-restore --filter AnalysisReportCoordinatorTests`

Expected: compilation failure because `AnalysisReportCoordinator` does not exist.

- [ ] **Step 3: Implement the minimal coordinator**

The production constructor accepts `IReportService` and delegates to `GetOrCreateDraftReportAsync`. An internal delegate-based constructor allows deterministic tests. `EnsureReportExistsAsync` catches exceptions, logs them, and returns `false`.

- [ ] **Step 4: Integrate after successful analysis persistence**

Register the coordinator in dependency injection, inject it into `Step4AnalyzeViewModel`, and invoke it after `UpdateCurrentMeasurementStatusAsync(MeasurementStatus.Completed, ...)`. Use the current session operator through the measurement's `OperatorId`, and log whether the report record was prepared without changing `AnalysisState` on failure.

- [ ] **Step 5: Verify focused and complete tests**

Run:

```powershell
dotnet test BTFX.Tests/BTFX.Tests.csproj --no-restore --filter AnalysisReportCoordinatorTests
dotnet test BTFX.slnx --no-restore
dotnet build BTFX.slnx -c Debug --no-restore
```

Expected: all tests pass and the build has zero errors.

- [ ] **Step 6: Commit**

```powershell
git add BTFX/Services/Implementations/AnalysisReportCoordinator.cs BTFX/App.xaml.cs BTFX/ViewModels/Measurement/Step4AnalyzeViewModel.cs BTFX.Tests/AnalysisReportCoordinatorTests.cs
git commit -m "fix: create reports after successful analysis"
```
