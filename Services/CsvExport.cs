using System.Globalization;
using System.Text;

namespace FlorenBooksWeb.Services;

public static class CsvExport
{
    public const string ContentType = "text/csv; charset=utf-8";

    public static byte[] Create(IEnumerable<string[]> rows)
    {
        var builder = new StringBuilder();

        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',', row.Select(Escape)));
        }

        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray();
    }

    public static string FormatDate(DateTime? value)
    {
        return value?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public static string FormatAmount(decimal? value)
    {
        return value?.ToString("0.00", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains('"') && !value.Contains(',') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
