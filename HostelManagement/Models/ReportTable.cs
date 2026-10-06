namespace HostelManagement.Models;

/// <summary>How a report column is formatted (and stored in Excel).</summary>
public enum ReportValueKind
{
    Text,
    Number,
    Money,
    Date,
}

/// <summary>One column of a report. The width is relative to the other columns.</summary>
public sealed record ReportColumn(string Header, ReportValueKind Kind = ReportValueKind.Text, double Width = 1);

/// <summary>Normal rows, bold total lines and bold group lines (for example a student before their invoices).</summary>
public enum ReportRowStyle
{
    Normal,
    Group,
    Subtotal,
    Total,
}

/// <summary>One row: a value per column (string, int, decimal, DateTime or null).</summary>
public sealed record ReportRow(object?[] Values, ReportRowStyle Style = ReportRowStyle.Normal);

/// <summary>A report as a table, written to the screen, to PDF and to Excel from the same data.</summary>
public sealed class ReportTable
{
    public string Title { get; init; } = string.Empty;

    /// <summary>Hostel, filters and date, shown under the title.</summary>
    public List<string> Subtitles { get; init; } = [];

    public List<ReportColumn> Columns { get; init; } = [];
    public List<ReportRow> Rows { get; init; } = [];

    /// <summary>A file name without extension, for example "Payments_2026-10-01_to_2026-10-31".</summary>
    public string FileName { get; init; } = "Report";

    public bool Landscape { get; init; } = true;
}
