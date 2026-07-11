# Data Safety and Consistency Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent database loss, create consistent SQLite backups, keep completed measurements and reports aligned with the latest analysis, and clean remaining localization and deployment issues.

**Architecture:** Introduce focused file/database helpers for recovery, snapshot creation, pending restore, and analysis finalization. Keep UI behavior unchanged while moving destructive or cross-table operations into testable services and transactions.

**Tech Stack:** .NET 10, WPF, Microsoft.Data.Sqlite, SqlSugar, xUnit, Inno Setup.

## Global Constraints

- Existing databases must never be deleted automatically after initialization failure.
- Online update and activation behavior must not change in this implementation.
- GPU algorithm `_internal` remains an external deployment dependency.
- Existing report preview, export, print, camera capture, and analysis execution behavior must remain compatible.

---

### Task 1: Database initialization recovery

**Files:**
- Create: `BTFX/Data/DatabaseRecoveryManager.cs`
- Modify: `BTFX/Data/DatabaseInitializer.cs`
- Test: `BTFX.Tests/DatabaseRecoveryTests.cs`

**Interfaces:**
- Produces: `DatabaseRecoveryManager.CaptureExistingDatabase(string databasePath)` returning the recovery directory.
- Produces: an internal `DatabaseInitializer(string databasePath, ILogHelper? logHelper = null)` constructor for deterministic tests.

- [ ] Write tests proving an invalid existing database is preserved and copied to `Data/Recovery`, while initialization throws.
- [ ] Run `dotnet test BTFX.Tests/BTFX.Tests.csproj --filter DatabaseRecoveryTests` and verify failure before implementation.
- [ ] Remove automatic delete/rebuild and forced GC from initialization; make `PRAGMA user_version` failures propagate.
- [ ] Run focused tests and commit with `fix: preserve databases when initialization fails`.

### Task 2: WAL-safe backup and staged restore

**Files:**
- Create: `BTFX/Data/SqliteSnapshotService.cs`
- Create: `BTFX/Data/PendingRestoreManager.cs`
- Modify: `BTFX/Services/Implementations/BackupService.cs`
- Modify: `BTFX/App.xaml.cs`
- Test: `BTFX.Tests/SqliteBackupRestoreTests.cs`

**Interfaces:**
- Produces: `SqliteSnapshotService.CreateSnapshotAsync(sourcePath, destinationPath, CancellationToken)`.
- Produces: `SqliteSnapshotService.ValidateAsync(databasePath, CancellationToken)`.
- Produces: `PendingRestoreManager.Stage(...)` and `ApplyIfPresent(...)`.

- [ ] Write a WAL-mode test that inserts data and verifies an online snapshot contains it.
- [ ] Write tests that reject corrupt restore databases and atomically apply valid pending restores.
- [ ] Run focused tests and verify failure before implementation.
- [ ] Replace raw database `File.Copy` backup with `BackupDatabase`, validate before ZIP creation, add a backup semaphore and unique temporary directories.
- [ ] Stage restore content instead of overwriting the live database; apply pending restore before database initialization on the next startup.
- [ ] Prevent automatic backup timer restart after disposal.
- [ ] Run focused and complete tests and commit with `fix: make database backup and restore consistent`.

### Task 3: Latest-analysis report and status consistency

**Files:**
- Create: `BTFX/Services/Implementations/AnalysisCompletionPersistenceService.cs`
- Create: `BTFX/Services/Implementations/AnalysisConsistencyRepairService.cs`
- Modify: `BTFX/Services/Implementations/AnalysisReportCoordinator.cs`
- Modify: `BTFX/ViewModels/Measurement/Step4AnalyzeViewModel.cs`
- Modify: `BTFX/Data/DatabaseInitializer.cs`
- Modify: `BTFX/App.xaml.cs`
- Test: `BTFX.Tests/AnalysisCompletionPersistenceTests.cs`
- Modify test: `BTFX.Tests/AnalysisReportCoordinatorTests.cs`

**Interfaces:**
- Produces: `FinalizeAsync(MeasurementRecord measurement, AnalysisResult result)` that transactionally updates measurement status and upserts one report linked to `result.Id`.
- Produces: `RepairAsync()` that aligns completed measurements and reports with each measurement's latest successful result.

- [ ] Write tests proving finalization creates one report, repeated finalization does not duplicate it, and reanalysis switches `AnalysisResultId` to the latest result.
- [ ] Run focused tests and verify failure before implementation.
- [ ] Add database v8 migration that removes duplicate reports and creates a unique `Reports(MeasurementId)` index.
- [ ] Replace the existing report-only coordinator call with transactional completion finalization.
- [ ] Run consistency repair once after dependency injection startup.
- [ ] Run focused and complete tests and commit with `fix: keep reports aligned with latest analysis`.

### Task 4: Localization and corrupted text cleanup

**Files:**
- Modify: `BTFX/Services/Implementations/MeasurementWorkflowResumeService.cs`
- Modify: `BTFX/ViewModels/CameraCaptureDialogViewModel.cs`
- Modify: `BTFX/Resources/Localization/Strings.zh.xaml`
- Modify: `BTFX/Resources/Localization/Strings.en.xaml`

- [ ] Add matching Chinese and English resource keys for workflow resume actions and messages.
- [ ] Inject localization into the resume service and replace hard-coded Chinese values.
- [ ] Replace the four corrupted camera log messages with readable localized or neutral text.
- [ ] Update the stale default-password text to `688626`.
- [ ] Compare resource key sets and commit with `fix: complete remaining localized workflow text`.

### Task 5: Installer and publish cleanup

**Files:**
- Modify: `BTFX/BTFX.csproj`
- Modify: `BTFX/Installer/BTFX.iss`
- Modify: `BTFX/Installer/README.md`

- [ ] Remove `ffplay.exe` from output and publish metadata.
- [ ] Exclude `Data/Config/report-reference-ranges.json` from wildcard overwrite and install it separately with `onlyifdoesntexist`.
- [ ] Document Galaxy SDK and algorithm `_internal` deployment requirements.
- [ ] Publish Release to a temporary directory and verify required files, reduced size, and absent `_internal`.
- [ ] Run all tests, Debug build, NuGet vulnerability scan, `git diff --check`, and commit with `build: harden installer data handling`.
