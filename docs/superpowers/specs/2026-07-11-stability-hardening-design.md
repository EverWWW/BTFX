# BTFX Stability Hardening Design

## Goal

Resolve the confirmed lifecycle, data-consistency, security, and deployment risks in priority order without changing the existing user workflow or visual design.

## Scope And Order

1. Prevent camera work from restarting after the capture dialog closes.
2. Make measurement archive import transactional and verify every archived file.
3. Delete a measurement, all related database rows, and its local result files as one recoverable operation.
4. Release transient dialog subscriptions and media resources without forced garbage collection.
5. Cancel stale review-preview generation and isolate concurrent temporary files.
6. Protect remembered credentials with Windows DPAPI and migrate account password hashes to PBKDF2.
7. Detect missing runtime dependencies before camera capture or gait analysis starts.
8. Add focused regression tests for each corrected behavior.

## Camera Dialog Lifecycle

`CameraCaptureDialogViewModel` will own a dialog-lifetime cancellation source and a terminal closing flag. Closing the dialog sets the flag before canceling recording, monitoring, preview, and delayed restart work. Preview startup and every cancellation/error continuation must check the terminal flag before opening a camera or starting FFmpeg.

Cleanup must be idempotent so repeated `Unloaded`, close-command, and exception paths cannot reopen or double-dispose a device. The existing capture result behavior remains unchanged when the user confirms a completed recording.

## Archive Import

An import uses a unique staging directory under `Data/Temp`. Before database insertion, all declared files are extracted into staging, constrained with `Path.GetRelativePath`, and verified against the manifest SHA256 and size. Missing, mismatched, or undeclared paths fail the import.

All database inserts for the package run in one transaction. After validation, staged files are moved into the final imported-result directory and paths are written to database entities. Any cancellation or exception rolls back database changes and removes staging and newly created final directories. Existing files and measurements are never overwritten.

Patient matching continues to follow the current import behavior in this hardening pass; changing identity rules is outside this scope.

## Measurement Deletion

Deletion includes reports, analysis CSV metadata, quality-control rows, kinematic summaries, analysis results, gait parameters, the measurement row, and all result-package files owned by that measurement.

Before opening the database transaction, owned result directories are resolved and checked to remain under application-managed result roots. During deletion they are moved to a unique quarantine directory under `Data/Temp/DeleteQuarantine`. Database rows are then deleted inside one transaction. On transaction failure, quarantined directories are restored. On success, quarantine is deleted asynchronously after the database commit. Failure to permanently remove quarantine is logged and can be retried without restoring deleted records.

Original imported or recorded source videos outside measurement-owned result directories are not deleted. Batch deletion applies the same rule per selected measurement and reports partial filesystem-cleanup warnings explicitly.

## Dialog And Media Resource Lifetime

Transient view models that subscribe to singleton services implement `IDisposable` and unsubscribe with named handlers. Their owning dialog views dispose them on unload exactly once. Media elements are stopped, detached, and cleared, but explicit `GC.Collect` calls are removed.

## Review Preview Generation

Each reload receives a generation number and cancellation token. A later reload or view unload cancels the earlier FFmpeg process and prevents stale completion from assigning a media source. Temporary output names are unique per generation. Only the active generation may promote a completed proxy into the deterministic cache path.

## Credential Security

Remembered login credentials use `ProtectedData` with `DataProtectionScope.CurrentUser`. Existing AES ciphertext is read only as a migration fallback and is rewritten with DPAPI after successful decryption.

Account passwords use PBKDF2-SHA256 with a per-user random salt and an explicit iteration count encoded with the stored value. Existing salted SHA256 and legacy hashes remain verifiable. A successful login using an old format immediately upgrades the stored hash.

## Deployment Diagnostics

The application retains the current manual deployment policy for the algorithm `_internal` directory and Daheng native runtime. Before entering analysis or opening a Daheng camera, a preflight service verifies FFmpeg/FFprobe, configured algorithm executable, algorithm runtime directory, managed Daheng assembly, and native Daheng runtime availability. Failures are localized, logged with exact missing paths/components, and shown without crashing the window.

## Verification

Focused tests cover:

- close/cancel cannot restart camera preview;
- archive checksum, missing file, path escape, cancellation, and rollback;
- complete database and filesystem measurement deletion plus rollback;
- transient localization subscriptions are released;
- stale preview generations cannot publish output;
- DPAPI round trip and legacy credential/password migration;
- dependency preflight results.

Every phase must pass its focused tests and the full solution test suite. Final verification includes Debug build, Release publish, package vulnerability scan, and a clean Git worktree review.

## Non-Goals

- No UI redesign.
- No change to camera synchronization or encoding parameters.
- No change to archive patient identity matching.
- No bundling of the multi-gigabyte algorithm runtime or Daheng driver into the installer.
