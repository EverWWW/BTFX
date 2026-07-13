# Custom Message Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace all ordinary system message boxes in the BTFX main application with a bilingual project-styled modal dialog.

**Architecture:** A synchronous `AppDialog` facade preserves existing call semantics and opens a custom owner-sized modal window. Application-owned enums isolate business code from WPF `MessageBox` types, while source-policy tests prevent regressions.

**Tech Stack:** .NET 10, WPF, MaterialDesignThemes, xUnit.

## Global Constraints

- Keep `ActivationCodeTool` system message boxes unchanged.
- Keep native open/save/folder/print dialogs unchanged.
- Preserve every existing business branch and localized message.
- Do not depend on `RootDialog`, because messages may appear during startup or inside an existing dialog.

---

### Task 1: Dialog Contract and Policy Tests

**Files:**
- Create: `BTFX.Tests/AppDialogPolicyTests.cs`
- Create: `BTFX/Helpers/AppDialogTypes.cs`

**Interfaces:**
- Produces: `AppDialogButtons`, `AppDialogIcon`, and `AppDialogResult`.

- [ ] Write tests asserting localized button resource keys and rejecting `MessageBox.Show` in main-project source files.
- [ ] Run the focused test and verify it fails because the contract/facade does not exist and legacy calls remain.
- [ ] Add the minimal enum contract needed by the UI task.
- [ ] Commit the test and contract.

### Task 2: Custom Dialog Window and Facade

**Files:**
- Create: `BTFX/Views/Dialogs/AppMessageDialogWindow.xaml`
- Create: `BTFX/Views/Dialogs/AppMessageDialogWindow.xaml.cs`
- Create: `BTFX/Helpers/AppDialog.cs`
- Modify: `BTFX/Resources/Localization/Strings.zh.xaml`
- Modify: `BTFX/Resources/Localization/Strings.en.xaml`

**Interfaces:**
- Consumes: `AppDialogButtons`, `AppDialogIcon`, `AppDialogResult`.
- Produces: `AppDialog.Show(string message, string title, AppDialogButtons buttons, AppDialogIcon icon)`.

- [ ] Add failing focused tests for button labels and default close results.
- [ ] Implement the owner-sized overlay window, localized buttons, icon variants, and modal result handling.
- [ ] Run focused tests and build the main project.
- [ ] Commit the reusable dialog infrastructure.

### Task 3: Migrate Main Application Call Sites

**Files:**
- Modify: the 16 audited main-project files containing `MessageBox.Show`.

**Interfaces:**
- Consumes: `AppDialog.Show` and application-owned enums.

- [ ] Replace informational, warning, and error calls without changing business branches.
- [ ] Replace Yes/No and OK/Cancel result comparisons with `AppDialogResult`.
- [ ] Remove obsolete `System.Windows.MessageBox` enum references and imports.
- [ ] Run the source-policy test until all 90 main-project calls are removed.
- [ ] Commit the migration.

### Task 4: Final Verification

**Files:**
- Modify only files required by verification failures.

- [ ] Verify Chinese and English localization dictionaries have identical keys.
- [ ] Run `dotnet test BTFX.slnx --no-restore`.
- [ ] Run a clean Debug build.
- [ ] Verify `ActivationCodeTool` retains its five system message boxes and BTFX retains none.
- [ ] Run `git diff --check` and confirm the worktree contains only intended changes.
