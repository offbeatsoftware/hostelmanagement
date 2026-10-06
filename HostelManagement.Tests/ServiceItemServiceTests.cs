using HostelManagement.Models;
using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class ServiceItemServiceTests : TestDatabase
{
    private ServiceItem Transport => ServiceItemService.GetServices(HostelId).Single(s => s.ServiceName == "Transport");

    private ServiceItem SetTransportRate(decimal rate)
    {
        ServiceItem transport = Transport;
        transport.MonthlyRate = rate;
        return ServiceItemService.Save(transport);
    }

    private int NewStudent(DateTime admission, int? collegeId = null, IReadOnlyCollection<int>? services = null) =>
        StudentService.Save(
            new Student { StudentName = "Aman", Gender = "Male", Mobile = "9876543210", CollegeId = collegeId ?? CollegeId, AdmissionDate = admission },
            new Parent { ParentName = "Parent", Mobile = "9812345678", Email = "parent@example.com" },
            extraServiceIds: services).StudentId;

    private void SetServices(int studentId, params int[] serviceIds) =>
        StudentService.Save(StudentService.GetStudent(studentId)!, StudentService.GetPrimaryParent(studentId)!,
            extraServiceIds: serviceIds);

    // ---- Services ----

    [Fact]
    public void NewHostel_HasWifiAndLaundryIncludedAndTransportExtra()
    {
        List<ServiceItem> services = ServiceItemService.GetServices(HostelId);

        Assert.Equal(["Laundry", "Transport", "Wi-Fi"], services.Select(s => s.ServiceName));
        Assert.True(services.Single(s => s.ServiceName == "Wi-Fi").IsIncludedInRent);
        Assert.True(services.Single(s => s.ServiceName == "Laundry").IsIncludedInRent);
        Assert.False(Transport.IsIncludedInRent);
        Assert.All(services, s => Assert.True(s.IsActive));
        Assert.Equal(["Laundry", "Wi-Fi"], ServiceItemService.GetIncludedServices(HostelId).Select(s => s.ServiceName));
    }

    [Fact]
    public void ServicesAndRatesAreSeparateForEachHostel()
    {
        int other = AddHostel("Other Hostel");
        SetTransportRate(1500m);

        Assert.Equal(1500m, Transport.MonthlyRate);
        Assert.Equal(0m, ServiceItemService.GetServices(other).Single(s => s.ServiceName == "Transport").MonthlyRate);
    }

    [Fact]
    public void Save_ExtraServiceNeedsAMonthlyRate_IncludedServiceHasNoCharge()
    {
        Assert.Contains("monthly rate", Assert.Throws<ValidationException>(() => SetTransportRate(0m)).Message);
        Assert.Contains("two decimal", Assert.Throws<ValidationException>(() => SetTransportRate(10.555m)).Message);

        ServiceItem gym = ServiceItemService.Save(new ServiceItem
        {
            HostelId = HostelId, ServiceName = "Gym", IsIncludedInRent = true, MonthlyRate = 999m,
        });
        Assert.Equal(0m, gym.MonthlyRate);
    }

    [Fact]
    public void Save_DuplicateNameInHostel_IsRejected()
    {
        var ex = Assert.Throws<ValidationException>(() =>
            ServiceItemService.Save(new ServiceItem { HostelId = HostelId, ServiceName = "wi-fi", IsIncludedInRent = true }));

        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void ServiceInUse_CannotBeDeactivatedMadeIncludedOrDeleted()
    {
        ServiceItem transport = SetTransportRate(1500m);
        int student = NewStudent(DateTime.Today.AddDays(-30), services: [transport.ServiceId]);
        Assert.Equal(1, Transport.StudentCount);

        ServiceItem inactive = Transport;
        inactive.IsActive = false;
        Assert.Contains("use Transport", Assert.Throws<ValidationException>(() => ServiceItemService.Save(inactive)).Message);

        ServiceItem included = Transport;
        included.IsIncludedInRent = true;
        Assert.Throws<ValidationException>(() => ServiceItemService.Save(included));

        SetServices(student);
        Assert.Contains("make it inactive", Assert.Throws<ValidationException>(() =>
            ServiceItemService.Delete(transport.ServiceId)).Message);

        ServiceItem stopped = Transport;
        stopped.IsActive = false;
        ServiceItemService.Save(stopped);
        Assert.False(Transport.IsActive);
    }

    [Fact]
    public void Delete_ServiceNeverUsed_Succeeds()
    {
        ServiceItemService.Delete(Transport.ServiceId);

        Assert.Equal(2, ServiceItemService.GetServices(HostelId).Count);
    }

    // ---- Student extra services ----

    [Fact]
    public void NewStudent_ServiceStartsOnTheAdmissionDate()
    {
        ServiceItem transport = SetTransportRate(1500m);
        DateTime admission = DateTime.Today.AddDays(-40);

        int student = NewStudent(admission, services: [transport.ServiceId]);

        StudentServiceUse use = StudentService.GetCurrentServices(student).Single();
        Assert.Equal("Transport", use.ServiceName);
        Assert.Equal(admission, use.StartDate);
        Assert.Null(use.EndDate);
        Assert.Equal(1500m, use.MonthlyRate);
    }

    [Fact]
    public void ExistingStudent_ServiceStartsTodayAndStopsToday()
    {
        ServiceItem transport = SetTransportRate(1500m);
        int student = NewStudent(DateTime.Today.AddDays(-40));

        SetServices(student, transport.ServiceId);
        Assert.Equal(DateTime.Today, StudentService.GetCurrentServices(student).Single().StartDate);

        // Started and removed on the same day: no record is kept.
        SetServices(student);
        Assert.Empty(StudentService.GetCurrentServices(student));
        Assert.Equal(0, Count("StudentService"));
    }

    [Fact]
    public void RemovingAServiceUsedBefore_KeepsItsHistoryWithAnEndDate()
    {
        ServiceItem transport = SetTransportRate(1500m);
        int student = NewStudent(DateTime.Today.AddDays(-40), services: [transport.ServiceId]);

        SetServices(student);

        Assert.Empty(StudentService.GetCurrentServices(student));
        Assert.Equal(1, Count("StudentService"));
        Assert.Equal(DateTime.Today, Convert.ToDateTime(Data.Db.Scalar("SELECT [EndDate] FROM [StudentService]")));
    }

    [Fact]
    public void OnlyActiveExtraServicesOfTheStudentsHostel_CanBeSelected()
    {
        int student = NewStudent(DateTime.Today.AddDays(-10));
        int wifi = ServiceItemService.GetServices(HostelId).Single(s => s.ServiceName == "Wi-Fi").ServiceId;
        int otherTransport = ServiceItemService.GetServices(AddHostel("Other Hostel")).Single(s => s.ServiceName == "Transport").ServiceId;

        Assert.Throws<ValidationException>(() => SetServices(student, wifi));
        Assert.Throws<ValidationException>(() => SetServices(student, otherTransport));
        Assert.Empty(StudentService.GetCurrentServices(student));
    }

    [Fact]
    public void DeletingAStudent_RemovesTheirServiceRecords()
    {
        ServiceItem transport = SetTransportRate(1500m);
        int student = NewStudent(DateTime.Today.AddDays(-10), services: [transport.ServiceId]);

        StudentService.Delete(student);

        Assert.Equal(0, Count("StudentService"));
    }
}
