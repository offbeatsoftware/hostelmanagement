# Hostel Management System

A single admin Windows desktop application for managing a private student hostel.

**Stack:** C# · .NET 8 · Windows Forms · Microsoft Access (.accdb) · System.Data.OleDb

**Status:** Phase 6 (room allocation: check-in, transfer, check-out, history), with several hostels. Module screens show a placeholder until their phase is built.

## Requirements

- Windows 10 or Windows 11
- Visual Studio 2022 (17.8 or later) with the **.NET desktop development** workload
- .NET 8 SDK (installed with that workload)
- **Microsoft Access Database Engine, 64 bit.** Start the application first: if it reports that the engine
  was not found, install the *Microsoft Access Database Engine 2016 Redistributable*
  (`accessdatabaseengine_X64.exe`) from Microsoft. Microsoft 365 (Click-to-Run) Office does not always
  make its engine available to other programs, so the redistributable may be needed even with Office installed.
  Microsoft Access itself is not required.

## Open, build and run

1. Clone the repository, or download and extract it.
2. Double click `HostelManagement.sln` to open it in Visual Studio.
3. In the toolbar, make sure the configuration is **Debug** and the platform is **x64**.
4. Build with **Build > Build Solution** (Ctrl+Shift+B). The Output window should show `Build succeeded`.
5. Run with **Debug > Start Debugging** (F5).

From the command line: `dotnet build HostelManagement.sln` and `dotnet run --project HostelManagement`.

NuGet package: `System.Data.OleDb` 8.0.1 (restored automatically on build).

## Signing in and choosing a hostel

The application opens with a sign in screen, where the admin also chooses the hostel to work on. The hostel
can be changed at any time in the box at the top right of the main window; colleges, rooms, students and
billing screens show only the selected hostel. Hostels are added on the **Hostels** screen. Temporary fixed credentials: user name **admin**, password **admin**
(the user name is not case sensitive). A changeable password will replace this before go-live.

## Automated tests

`HostelManagement.Tests` (xUnit) builds a fresh database in a temporary folder for every test, so it never
touches the application's own database. Run them in Visual Studio with **Test > Run All Tests** (platform x64). GitHub Actions
runs them on Windows for every push (`.github/workflows/build-and-test.yml`).

## 32 bit vs 64 bit

The application is built as **x64**, matching the hostel PC's 64 bit Office. The Access Database Engine (ACE OLE DB provider) must have the same
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
  Models/                 Data classes (from Phase 3)
  Data/                   Access database: connection helper (Db), schema, startup initializer
  Services/               Business logic: database check; allocation, billing, email in later phases
  Reports/                Invoice and report output (later phases)
  Utilities/              Paths, logging, dialogs, error handling, UI theme
```

All data is kept in folders **next to `HostelManagement.exe`**, so it is part of the installed application folder.
During development that is `HostelManagement\bin\x64\Debug\net8.0-windows\`. The folders are created
automatically on first start:

| Folder               | Purpose                                         |
|----------------------|-------------------------------------------------|
| `Database`           | `HostelManagement.accdb`. The build copies the empty database from `HostelManagement/Database` here only when none exists, so a rebuild never overwrites data. |
| `Photos/Students`    | Student photos (only the file path is stored in the database) |
| `Documents/Students` | Student documents (only the file path is stored in the database) |
| `Backups`            | Database backups                                |
| `Logs`               | Technical error logs (`app-yyyyMMdd.log`)       |

**Installation note:** Windows does not let normal users write inside `C:\Program Files`, so the application
must be installed in a folder the admin can write to (for example `C:\HostelManagement`). The setup will do this.

These folders contain real student data and must never be committed to Git.

The database design is documented in [docs/DatabaseSchema.md](docs/DatabaseSchema.md).
To check the database at any time, open **Application Settings** and click **Test Database**.

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
- **Data access:** no SQL in forms. Forms call services/data classes, which use `Db` with `?` placeholders
  and `Db.Param(...)` (positional, in placeholder order). Bracket all table and column names.
  Use `Db.InTransaction` for actions that change more than one row or table.
- **Tab order:** set a logical `TabIndex` on every input control, top to bottom, left to right.
