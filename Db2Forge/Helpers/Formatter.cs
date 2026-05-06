using System.Reflection;

namespace Db2Forge.Helpers;

public static class Formatter
{
    public static string FormatRawValue(object? value)
    {
        if (value == null) return "NULL";
        if (value is string s) return $"'{s.Replace("'", "''")}'";
        if (value is DateTime dt) return dt.ToString("yyyyMMdd");
        return value.ToString() ?? string.Empty;
    }

    public static string FormatValue(PropertyInfo prop, object? value)
    {
        var nonNullableType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (nonNullableType == typeof(DateTime) && value == null)
            return "0";

        if (value == null) return "NULL";

        // DateTime → decimal AS400 yyyyMMdd
        if (nonNullableType == typeof(DateTime))
            return ((DateTime)value).ToString("yyyyMMdd");

        // String → entouré de quotes
        if (nonNullableType == typeof(string))
            return $"'{(value.ToString() ?? string.Empty).Replace("'", "''")}'"; // escape les quotes

        if (nonNullableType == typeof(decimal))
            return (value.ToString() ?? string.Empty).Replace(",", ".");

        // decimal, int, etc. → valeur brute
        return value.ToString() ?? string.Empty;
    }
}