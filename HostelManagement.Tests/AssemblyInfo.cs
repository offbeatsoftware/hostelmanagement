// The application's database settings are static, so tests must not run in parallel.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace HostelManagement.Tests
{
    internal static class TestSetup
    {
        // A message box would block a test run forever: make it throw instead. Connection pooling is off
        // because UI tests open the database on short lived window threads (see Db.DisablePooling).
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void Initialize()
        {
            Utilities.Dialogs.ThrowInsteadOfShowing = true;
            Data.Db.DisablePooling = true;
            // The main window is opened and closed many times; no daily backup in the tests.
            Services.BackupService.AutomaticBackupEnabled = false;
        }
    }
}
