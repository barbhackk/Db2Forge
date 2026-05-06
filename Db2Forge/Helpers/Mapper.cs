using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;

namespace Db2Forge.Helpers;

public class Mapper
{
    public static List<T> MapDataSet<T>(DataSet ds) where T : class
    {
        var result = new List<T>();

        if (ds == null || ds.Tables.Count == 0)
            return result;

        var table = ds.Tables[0];
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (DataRow row in table.Rows)
        {
            var item = Activator.CreateInstance<T>();

            foreach (var prop in properties)
            {
                var column = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

                if (!table.Columns.Contains(column))
                    continue;

                var value = row[column];

                if (value == DBNull.Value)
                    continue;

                var convertedValue = ConvertValue(value, prop.PropertyType);

                prop.SetValue(item, convertedValue);
            }

            result.Add(item);
        }

        return result;
    }

    public static T? MapOneDataSet<T>(DataSet ds) where T : class
    {
        var result = Activator.CreateInstance<T>();

        if (ds == null || ds.Tables.Count == 0)
            return null;

        var table = ds.Tables[0];
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        if (table.Rows.Count == 0)
            return null;

        DataRow row = table.Rows[0];

        foreach (var prop in properties)
        {
            var column = prop.GetCustomAttribute<ColumnAttribute>()?.Name ?? prop.Name;

            if (!table.Columns.Contains(column))
                continue;

            var value = row[column];

            var convertedValue = ConvertValue(value, prop.PropertyType);

            prop.SetValue(result, convertedValue);
        }

        return result;
    }

    private static object? ConvertValue(object value, Type targetType)
    {
        var nonNullableType = Nullable.GetUnderlyingType(targetType) ?? targetType;

        if (value == DBNull.Value && nonNullableType != typeof(string))
            return null;

        // decimal → DateTime
        if (nonNullableType == typeof(DateTime) && value is decimal dec)
        {
            if (dec <= 0 && Nullable.GetUnderlyingType(targetType) != null)
            {
                return null;
            }
            else return ConvertDecimalToDate(dec);
        }

        if (nonNullableType == typeof(string))
        {
            return (value.ToString() ?? string.Empty).ToString().Trim();
        }

        return Convert.ChangeType(value, nonNullableType);
    }

    private static DateTime ConvertDecimalToDate(decimal value)
    {
        int val = (int)value; // ignore les décimales
        int year = val / 10000;
        int month = (val / 100) % 100;
        int day = val % 100;

        // vérifie que c'est valide
        if (year == 0 || month == 0 || day == 0)
            return DateTime.MinValue;

        return new DateTime(year, month, day);
    }
}