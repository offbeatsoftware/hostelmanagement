// The application's database settings are static, so tests must not run in parallel.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace HostelManagement.Tests
{
    internal static class TestSetup
    {
        // A message box would block a test run forever; make it throw instead.
        [System.Runtime.CompilerServices.ModuleInitializer]
        internal static void DisableMessageBoxes() => Utilities.Dialogs.ThrowInsteadOfShowing = true;
    }
}
