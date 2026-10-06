namespace HostelManagement.Tests;

/// <summary>
/// One window thread for every UI test, alive for the whole test run with a message loop, like the
/// application's own main thread. Windows created on short lived threads could receive system notifications
/// (for example a settings change) after their thread had ended, which crashed the test host at random
/// (exit code 0xC000041D).
/// </summary>
internal static class UiThread
{
    private static readonly Lazy<SynchronizationContext> Context = new(Start);
    private static Exception? s_windowException;

    /// <summary>Runs the action on the UI thread and fails the test with any exception, including one raised in a window message.</summary>
    public static void Run(Action action)
    {
        Exception? failure = null;
        using var done = new ManualResetEventSlim();
        Context.Value.Post(_ =>
        {
            s_windowException = null;
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
            failure ??= s_windowException;
            done.Set();
        }, null);

        if (!done.Wait(TimeSpan.FromMinutes(2)))
        {
            throw new TimeoutException("The UI test did not finish (a message box may be open).");
        }
        if (failure is not null)
        {
            throw new Xunit.Sdk.XunitException($"UI test failed: {failure}");
        }
    }

    private static SynchronizationContext Start()
    {
        SynchronizationContext? context = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            // An exception inside a window message is reported as a failure of the running test.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException, threadScope: true);
            Application.ThreadException += (_, e) => s_windowException ??= e.Exception;

            context = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(context);
            ready.Set();
            Application.Run();
        })
        {
            IsBackground = true,
            Name = "UI tests",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return context!;
    }
}
