using ClosedXML.Excel;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Reports;

/// <summary>
/// Writes any <see cref="ReportTable"/> as an Excel workbook (.xlsx): title lines, a frozen header row with
/// filters, and real numbers and dates so the admin can sort, filter and add up in Excel.
/// </summary>
public static class ReportExcelWriter
{
    // Rupees with Indian digit grouping: ₹1,20,000.00 and ₹1,00,00,000.00.
    private const string RupeeFormat =
        "[>=10000000]\"₹\"##\\,##\\,##\\,##0.00;[>=100000]\"₹\"##\\,##\\,##0.00;\"₹\"#,##0.00";

    public static void Write(ReportTable report, string path)
    {
        using var workbook = new XLWorkbook();
        IXLWorksheet sheet = workbook.Worksheets.Add(SheetName(report.Title));

        int row = 1;
        sheet.Cell(row, 1).Value = $"{AppInfo.BusinessName}: {report.Title}";
        sheet.Cell(row, 1).Style.Font.Bold = true;
        sheet.Cell(row, 1).Style.Font.FontSize = 14;
        row++;
        foreach (string subtitle in report.Subtitles.Where(s => s.Length > 0))
        {
            sheet.Cell(row, 1).Value = subtitle;
            sheet.Cell(row, 1).Style.Font.FontColor = XLColor.Gray;
            row++;
        }
        row++;

        int headerRow = row;
        for (int c = 0; c < report.Columns.Count; c++)
        {
            IXLCell cell = sheet.Cell(headerRow, c + 1);
            cell.Value = report.Columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(245, 236, 220);
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        foreach (ReportRow reportRow in report.Rows)
        {
            row++;
            for (int c = 0; c < report.Columns.Count; c++)
            {
                IXLCell cell = sheet.Cell(row, c + 1);
                SetValue(cell, reportRow.Values[c]);
                switch (report.Columns[c].Kind)
                {
                    case ReportValueKind.Money:
                        cell.Style.NumberFormat.Format = RupeeFormat;
                        break;
                    case ReportValueKind.Date:
                        cell.Style.DateFormat.Format = "dd-mmm-yyyy";
                        break;
                }
            }
            if (reportRow.Style != ReportRowStyle.Normal)
            {
                IXLRange range = sheet.Range(row, 1, row, report.Columns.Count);
                range.Style.Font.Bold = true;
                if (reportRow.Style == ReportRowStyle.Total)
                {
                    range.Style.Fill.BackgroundColor = XLColor.FromArgb(245, 236, 220);
                    range.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                }
            }
        }

        if (row > headerRow)
        {
            sheet.Range(headerRow, 1, row, report.Columns.Count).SetAutoFilter();
        }
        sheet.SheetView.FreezeRows(headerRow);
        sheet.Columns(1, report.Columns.Count).AdjustToContents(headerRow, row);
        foreach (IXLColumn column in sheet.Columns(1, report.Columns.Count))
        {
            column.Width = Math.Clamp(column.Width + 2, 8, 60);
        }

        workbook.SaveAs(path);
    }

    private static void SetValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case decimal amount:
                cell.Value = amount;
                break;
            case int number:
                cell.Value = number;
                break;
            case DateTime date:
                cell.Value = date;
                break;
            default:
                cell.Value = Convert.ToString(value) ?? string.Empty;
                break;
        }
    }

    /// <summary>Excel sheet names are at most 31 characters and cannot contain some symbols.</summary>
    private static string SheetName(string title)
    {
        string name = string.Concat(title.Where(c => !"[]:*?/\\".Contains(c)));
        return name.Length > 31 ? name[..31] : name;
    }
}
