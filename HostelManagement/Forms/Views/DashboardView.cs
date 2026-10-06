using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;

namespace HostelManagement.Forms.Views;

/// <summary>
/// Dashboard of the selected hostel: headline figures, payments received per month of the academic year,
/// and short lists of the most overdue students, the latest payments and the latest check-ins.
/// </summary>
public sealed class DashboardView : UserControl
{
    private readonly Hostel _hostel;
    private readonly TableLayoutPanel _tiles;
    private readonly PaymentsChart _chart;
    private readonly Label _chartTitle;
    private readonly DataGridView _overdueGrid;
    private readonly DataGridView _paymentsGrid;
    private readonly DataGridView _checkInsGrid;

    public DashboardView(Hostel hostel)
    {
        _hostel = hostel;
        Dock = DockStyle.Fill;
        BackColor = UiTheme.ContentBackground;

        // Six equal columns, so the figures stay on one row at any window width.
        _tiles = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 1, Height = 96, Margin = Padding.Empty };
        for (int i = 0; i < 6; i++)
        {
            _tiles.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 6));
        }

        _chart = new PaymentsChart { Dock = DockStyle.Fill };
        Panel chartCard = FormFields.CreateCard(string.Empty, _chart);
        _chartTitle = chartCard.Controls.OfType<Label>().Single();
        chartCard.Dock = DockStyle.Fill;
        chartCard.Margin = new Padding(0, 4, 0, 8);

        _overdueGrid = CreateListGrid();
        FormFields.AddGridColumn(_overdueGrid, nameof(StudentDue.StudentName), "Student", 48);
        FormFields.AddGridColumn(_overdueGrid, nameof(StudentDue.OverdueAmount), "Overdue", 32, format: "C0", alignRight: true);
        FormFields.AddGridColumn(_overdueGrid, nameof(StudentDue.DaysOverdue), "Days", 16, alignRight: true);

        _paymentsGrid = CreateListGrid();
        FormFields.AddGridColumn(_paymentsGrid, nameof(Payment.PaymentDate), "Date", 26, format: "dd MMM");
        FormFields.AddGridColumn(_paymentsGrid, nameof(Payment.StudentName), "Student", 42);
        FormFields.AddGridColumn(_paymentsGrid, nameof(Payment.Amount), "Amount", 32, format: "C0", alignRight: true);

        _checkInsGrid = CreateListGrid();
        FormFields.AddGridColumn(_checkInsGrid, nameof(RoomAllocation.CheckInDate), "Date", 26, format: "dd MMM");
        FormFields.AddGridColumn(_checkInsGrid, nameof(RoomAllocation.StudentName), "Student", 48);
        FormFields.AddGridColumn(_checkInsGrid, nameof(RoomAllocation.RoomNumber), "Room", 22);

        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Margin = Padding.Empty };
        for (int i = 0; i < 3; i++)
        {
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
        }
        lists.Controls.Add(ListCard("Most overdue students", _overdueGrid, new Padding(0, 0, 6, 0)), 0, 0);
        lists.Controls.Add(ListCard("Latest payments", _paymentsGrid, new Padding(3, 0, 3, 0)), 1, 0);
        lists.Controls.Add(ListCard("Latest check-ins", _checkInsGrid, new Padding(6, 0, 0, 0)), 2, 0);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = Padding.Empty };
        // Figures and lists keep their height (five rows per list); the chart gets the rest.
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 236));
        layout.Controls.Add(_tiles, 0, 0);
        layout.Controls.Add(chartCard, 0, 1);
        layout.Controls.Add(lists, 0, 2);
        Controls.Add(layout);

        Load += (_, _) => LoadDashboard();
    }

    private void LoadDashboard()
    {
        try
        {
            DashboardData data = DashboardService.Get(_hostel.HostelId, DateTime.Today);

            _tiles.SuspendLayout();
            _tiles.Controls.Clear();
            _tiles.Controls.Add(Tile("Students", data.ActiveStudents.ToString(Money.Culture),
                $"active, {data.LeftStudents} left"));
            _tiles.Controls.Add(Tile("Beds occupied", $"{data.OccupiedBeds} / {data.TotalBeds}",
                $"{data.FreeBeds} free"));
            _tiles.Controls.Add(Tile($"Invoiced {data.AcademicYear}", Money.FormatWhole(data.InvoicedThisYear),
                "academic year from July"));
            _tiles.Controls.Add(Tile($"Received in {data.AsOf:MMMM}", Money.FormatWhole(data.ReceivedThisMonth),
                $"{data.AsOf:MMMM yyyy}"));
            _tiles.Controls.Add(Tile("Pending", Money.FormatWhole(data.PendingAmount), "all unpaid invoices"));
            _tiles.Controls.Add(Tile("Overdue", Money.FormatWhole(data.OverdueAmount),
                $"{data.OverdueStudents} students, {Invoice.PaymentDueDays}+ days", overdue: data.OverdueAmount > 0));
            _tiles.ResumeLayout();

            _chartTitle.Text = $"Payments received per month, academic year {data.AcademicYear}";
            _chart.SetData(data.PaymentsPerMonth, data.AsOf);

            _overdueGrid.DataSource = data.MostOverdue;
            _paymentsGrid.DataSource = data.LatestPayments;
            _checkInsGrid.DataSource = data.RecentCheckIns;
        }
        catch (Exception ex)
        {
            ErrorHandler.Handle(ex, "The dashboard could not be loaded.");
        }
    }

    /// <summary>A headline figure: caption, big value and a short note. Overdue shows a warning sign, not colour alone.</summary>
    private static Panel Tile(string caption, string value, string note, bool overdue = false)
    {
        var tile = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 8, 8),
            Padding = new Padding(12, 8, 8, 8),
        };
        var captionLabel = new Label { Text = caption, Dock = DockStyle.Top, Height = 22, ForeColor = UiTheme.TextMuted, AutoEllipsis = true };
        var valueLabel = new Label
        {
            Text = overdue ? "⚠ " + value : value,
            Dock = DockStyle.Top,
            Height = 34,
            Font = UiTheme.HeadingFont,
            ForeColor = overdue ? UiTheme.Danger : UiTheme.TextPrimary,
            AutoEllipsis = true,
        };
        var noteLabel = new Label { Text = note, Dock = DockStyle.Top, Height = 20, ForeColor = UiTheme.TextMuted, AutoEllipsis = true };
        tile.Controls.Add(noteLabel);
        tile.Controls.Add(valueLabel);
        tile.Controls.Add(captionLabel);
        return tile;
    }

    private static DataGridView CreateListGrid()
    {
        var grid = new DataGridView { Dock = DockStyle.Fill, AutoGenerateColumns = false };
        UiTheme.StyleGrid(grid);
        grid.ScrollBars = ScrollBars.None;
        grid.TabStop = false;
        // A short list to read, not to pick from: no highlighted row.
        grid.DataBindingComplete += (_, _) => grid.ClearSelection();
        return grid;
    }

    private static Panel ListCard(string title, DataGridView grid, Padding margin)
    {
        Panel card = FormFields.CreateCard(title, grid);
        card.Dock = DockStyle.Fill;
        card.Margin = margin;
        return card;
    }
}
