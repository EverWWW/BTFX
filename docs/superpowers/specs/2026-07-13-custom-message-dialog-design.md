# Custom Message Dialog Design

## Scope

Replace every `System.Windows.MessageBox.Show` call in the BTFX main application with a project-owned dialog. Keep the internal `ActivationCodeTool`, native open/save file dialogs, and the native print dialog unchanged.

## Goals

- Use the project font, sizing, colors, rounded corners, hover states, and bilingual resources for every ordinary message and confirmation dialog.
- Preserve the current behavior of OK, OK/Cancel, and Yes/No call sites.
- Work before the main window is available and while another application dialog is already open.
- Centralize dialog creation so new code does not directly depend on `System.Windows.MessageBox`.

## Architecture

Add a synchronous `AppDialog` facade because the existing call sites use the synchronous semantics of `MessageBox.Show`. The facade creates a borderless modal `AppMessageDialogWindow`, resolves localized button text from application resources, selects an icon and colors from the requested dialog kind, and returns an application-owned result enum.

The dialog window covers the owner window with a translucent overlay when an owner is available. This avoids nested `DialogHost` failures for messages raised from camera capture, report preview, or other existing dialogs. During startup failures, it opens as a centered standalone dialog without requiring `MainWindow` or `RootDialog`.

## API

- `AppDialog.Show(message, title, buttons, icon)` returns `AppDialogResult`.
- `AppDialogButtons`: `Ok`, `OkCancel`, `YesNo`.
- `AppDialogIcon`: `Information`, `Warning`, `Error`, `Question`.
- `AppDialogResult`: `None`, `Ok`, `Cancel`, `Yes`, `No`.

Button labels use `OK`, `Cancel`, `Yes`, and `No` from the active localization dictionary. The dialog title and message remain supplied by each caller so existing localization and error detail behavior is preserved.

## UI

- White 8 px rounded card over a translucent dark owner-sized overlay.
- Centered title, visible close button, project `AppFont`.
- Title 30 px, message 22 px with wrapping, buttons 22 px.
- Information/question use the project purple, warning uses orange, and error uses red.
- Primary and secondary buttons use the same 16 px rounded style and hover behavior as other project dialogs.
- Long messages scroll within a bounded content area instead of expanding beyond the screen.

## Migration

Replace all 90 main-application `MessageBox.Show` calls and their enum/result checks. Do not modify the five calls in `ActivationCodeTool`. Do not replace `OpenFileDialog`, `SaveFileDialog`, `PrintDialog`, or folder selection dialogs.

## Verification

- Unit tests verify button localization/result mapping and source-policy enforcement.
- Static source test fails if `MessageBox.Show` is reintroduced under the BTFX main project.
- Full test suite and clean build must pass.
- Manual checks cover Chinese and English OK, OK/Cancel, and Yes/No dialogs, startup failure presentation, and confirmation opened from inside camera capture.
