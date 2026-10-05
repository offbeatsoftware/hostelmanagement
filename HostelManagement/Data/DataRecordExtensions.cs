using System.Data;

namespace HostelManagement.Data;

/// <summary>Null safe column readers for mapping query results to models.</summary>
public static class DataRecordExtensions
{
    public static int GetInt(this IDataRecord record, string column) =>
        Convert.ToInt32(record[column]);

    public static int? GetNullableInt(this IDataRecord record, string column) =>
        record[column] is DBNull ? null : Convert.ToInt32(record[column]);

    public static string GetText(this IDataRecord record, string column) =>
        record[column] is DBNull ? string.Empty : Convert.ToString(record[column]) ?? string.Empty;

    public static decimal GetMoney(this IDataRecord record, string column) =>
        record[column] is DBNull ? 0m : Convert.ToDecimal(record[column]);

    public static bool GetBool(this IDataRecord record, string column) =>
        record[column] is not DBNull && Convert.ToBoolean(record[column]);

    public static DateTime GetDate(this IDataRecord record, string column) =>
        Convert.ToDateTime(record[column]);

    public static DateTime? GetNullableDate(this IDataRecord record, string column) =>
        record[column] is DBNull ? null : Convert.ToDateTime(record[column]);
}
