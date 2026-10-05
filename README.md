# Hostel Management System

A single admin Windows desktop application for managing a private student hostel.

**Stack:** C# · .NET 8 · Windows Forms · Microsoft Access (.accdb) · Microsoft.Data.OleDb

**Status:** Phase 1 (project setup, main window, navigation). Module screens show a placeholder until their phase is built.

## Requirements

- Windows 10 or Windows 11
- Visual Studio 2022 (17.8 or later) with the **.NET desktop development** workload
- .NET 8 SDK (installed with that workload)
- From Phase 2 onwards: **Microsoft Access Database Engine 2016 Redistributable, x64**
  (not needed if 64 bit Microsoft Office/Access is already installed)

## Open, build and run

1. Clone the repository, or download and extract it.
2. Double click `HostelManagement.sln` to open it in Visual Studio.
3. In the toolbar, make sure the configuration is **Debug** and the platform is **x64**.
4. Build with **Build > Build Solution** (Ctrl+Shift+B). The Output window should show `Build succeeded`.
5. Run with **Debug > Start Debugging** (F5).

From the command line: `dotnet build HostelManagement.sln` and `dotnet run --project HostelManagement`.

## 32 bit vs 64 bit

The application is built as **x64**. The Access Database Engine (ACE OLE DB provider) must have the same
bitness as the application. If the hostel PC has **32 bit** Office installed, the 64 bit engine cannot be
installed alongside it; in that case change `<PlatformTarget>` and `<Platforms>` in
`HostelManagement/HostelManagement.csproj` to `x86` and use the 32 bit engine.

## Project structure

```
HostelManagement.sln
HostelManagement/
  Program.cs              Startup, folder creation, global error handling
  Forms/                  Main window, navigation and module screens
    Views/                Module screens (UserControls) shown inside the main window
  Models/                 Data classes (from Phase 2)
  Data/                   Access database access, parameterized queries only (from Phase 2)
  Services/               Business logic: allocation, billing, email (later phases)
  Reports/                Invoice and report output (later phases)
  Utilities/              Paths, logging, dialogs, error handling, UI theme
```

The following folders are created automatically next to `HostelManagement.exe` on first start:

| Folder               | Purpose                                         |
|----------------------|-------------------------------------------------|
| `Database`           | `HostelManagement.accdb`                        |
| `Photos/Students`    | Student photos (only the file path is stored in the database) |
| `Documents/Students` | Student documents (only the file path is stored in the database) |
| `Backups`            | Database backups                                |
| `Logs`               | Technical error logs (`app-yyyyMMdd.log`)       |

These folders contain real student data and are excluded from Git.

## UI conventions

All screens follow these rules so the application stays consistent and easy to use:

- **Layout:** each module is a `UserControl` in `Forms/Views`, shown in the main window's content area.
  Register new screens in `MainForm.BuildNavigation()`.
- **Fonts and colours:** use `UiTheme` only, never set fonts or colours directly on a screen.
- **Buttons:** `UiTheme.StylePrimaryButton` for the main action (Save, Add),
  `StyleSecondaryButton` for Cancel/Clear/Refresh, `StyleDangerButton` for Delete and Check-out.
- **Lists:** `DataGridView` styled with `UiTheme.StyleGrid` (read only, full row select), with a search box above it.
- **Messages:** `Dialogs.Info/Warning/Error/Confirm`. Destructive actions always call `Dialogs.Confirm`
  (No is the default button). Avoid popups for routine success messages; use the status bar instead.
- **Errors:** catch exceptions at the screen level and call `ErrorHandler.Handle(ex, "friendly message")`.
  The admin never sees a stack trace; details go to the log file.
- **Data access:** no SQL in forms. Forms call services/data classes, which use parameterized queries.
- **Tab order:** set a logical `TabIndex` on every input control, top to bottom, left to right.
