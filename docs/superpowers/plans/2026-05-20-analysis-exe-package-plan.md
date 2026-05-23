# Analysis Exe Simulation And Result Package Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a realistic mock algorithm executable and the upper-computer integration points for reading algorithm outputs, packaging results, and validating completed analysis data.

**Architecture:** Keep the existing `GaitAnalysisService` as the process orchestration boundary. Add a mock console executable that follows the same input/output contract as the future algorithm module, plus focused services for output reading and package creation/validation so later real-exe integration mostly changes parsing and field mapping.

**Tech Stack:** .NET 10, WPF, SqlSugar, JSON, CSV, ZIP via `System.IO.Compression`, SHA-256 via `System.Security.Cryptography`.

---

### Task 1: Mock Algorithm Executable

**Files:**
- Create: `MockGaitAnalysis/MockGaitAnalysis.csproj`
- Create: `MockGaitAnalysis/Program.cs`
- Modify: `BTFX.slnx`
- Modify: `BTFX/BTFX.csproj`

- [ ] Add a console project that accepts one argument: the path to `task_config.json`.
- [ ] Read the config as JSON and infer `output_directory`, `request_id`, and input videos.
- [ ] Simulate progress by printing log lines for several stages.
- [ ] Write `result.json`, `summary.json`, three CSV files, `log.txt`, and `annotated_video.mp4`.
- [ ] Copy the first available source video as `annotated_video.mp4`; if no video exists, create an empty placeholder file.
- [ ] Add the project to `BTFX.slnx`.
- [ ] Copy the built executable into `BTFX/bin/.../Algorithm/gait_analysis.exe` through a project reference or MSBuild target.

### Task 2: Algorithm Output Reader

**Files:**
- Create: `BTFX/Services/Interfaces/IAnalysisOutputReader.cs`
- Create: `BTFX/Services/Implementations/AnalysisOutputReader.cs`
- Modify: `BTFX/Services/Implementations/GaitAnalysisService.cs`
- Modify: `BTFX/App.xaml.cs`

- [ ] Add a reader service that resolves output files from a directory.
- [ ] Prefer `result.json`; fall back to `summary.json`.
- [ ] Map algorithm output into existing `AnalysisSummary` and `AnalysisResult` models.
- [ ] Detect and register three CSV files even if their names differ from the old `CsvFilesDto` fields.
- [ ] Keep the existing service behavior compatible with old `summary.json` outputs.

### Task 3: Analysis Result Package

**Files:**
- Create: `BTFX/Models/Analysis/AnalysisPackageManifest.cs`
- Create: `BTFX/Services/Interfaces/IAnalysisPackageService.cs`
- Create: `BTFX/Services/Implementations/AnalysisPackageService.cs`
- Modify: `BTFX/Models/Analysis/AnalysisResult.cs`
- Modify: `BTFX/Data/DatabaseInitializer.cs`
- Modify: `BTFX/App.xaml.cs`

- [ ] Add package fields to `AnalysisResult`: package path, validation status, and validation message.
- [ ] Add database migration for the new fields.
- [ ] Generate a `.btfxpkg` zip after analysis success is saved.
- [ ] Package `manifest.json`, `checksums.json`, `result.json`, CSV files, annotated video, and log file.
- [ ] Store SHA-256 checksums for package contents.
- [ ] Validate packages by checking file existence and checksums.

### Task 4: Analysis Flow Integration

**Files:**
- Modify: `BTFX/ViewModels/Measurement/Step4AnalyzeViewModel.cs`
- Modify: `BTFX/ViewModels/GaitAnalysisDetailViewModel.cs`
- Modify: `BTFX/ViewModels/ReportViewModel.cs`

- [ ] After saving an `AnalysisResult`, create the package and persist the package path on the analysis result.
- [ ] Before opening analysis detail or generating a report, validate the package if a path exists.
- [ ] If package validation fails, show a blocking warning and ask the user to re-analyze.
- [ ] Leave existing demo and fallback display data intact.

### Task 5: Verification

**Files:**
- Modify as needed from previous tasks.

- [ ] Run `dotnet build BTFX/BTFX.csproj -p:UseAppHost=false`.
- [ ] Run `dotnet build MockGaitAnalysis/MockGaitAnalysis.csproj`.
- [ ] Run the mock executable manually against a sample config and confirm files are produced.
- [ ] Run the app build again and confirm no compile errors.
