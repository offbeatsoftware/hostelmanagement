using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class DatabaseCheckServiceTests : TestDatabase
{
    [Fact]
    public void Run_AllStepsPass()
    {
        List<DatabaseCheckStep> steps = DatabaseCheckService.Run();

        Assert.Equal(
            ["Open connection", "Check tables", "Insert test record", "Read test record",
             "Update test record", "Delete test record"],
            steps.Select(step => step.Step));
        Assert.All(steps, step => Assert.True(step.Passed, $"{step.Step}: {step.Details}"));
    }

    [Fact]
    public void Run_LeavesNoTestData()
    {
        DatabaseCheckService.Run();
        DatabaseCheckService.Run();

        Assert.Equal(0, Count("Service"));
    }
}
