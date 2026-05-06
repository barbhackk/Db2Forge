
using Db2Forge.Helpers;
using Db2Forge.Query;

namespace Db2Forge.Core;

public class Repository<T>(string tableName, EntityManager manager) where T : class
{
    private readonly string _tableName = tableName;
    private readonly EntityManager _manager = manager;

    public string GetTableName()
    {
        return _tableName;
    }

    public List<T> FindAll()
    {
        var sql = $"SELECT * FROM {_tableName}";
        var dataset = _manager._dao.Fetch(sql);
        return Mapper.MapDataSet<T>(dataset);
    }

    public List<T> FindBy(Criteria criteria)
    {
        var conditions = new List<string>();

        foreach (var entry in criteria.GetEntries())
        {
            var formatted = Formatter.FormatRawValue(entry.Value);
            conditions.Add($"{entry.ColumnName} = {formatted}");
        }

        var sql = $"SELECT * FROM {_tableName} WHERE {string.Join(" AND ", conditions)}";
        var dataset = _manager._dao.Fetch(sql);
        return Mapper.MapDataSet<T>(dataset);
    }

    public T? FindOneBy(Criteria criteria)
    {
        var conditions = new List<string>();

        foreach (var entry in criteria.GetEntries())
        {
            var formatted = Formatter.FormatRawValue(entry.Value);
            conditions.Add($"{entry.ColumnName} = {formatted}");
        }

        var sql = $"SELECT * FROM {_tableName} WHERE {string.Join(" AND ", conditions)}";
        var dataset = _manager._dao.Fetch(sql);

        var result = Mapper.MapDataSet<T>(dataset);
        if (result.Count > 0)
        {
            return result[0];
        }
        else return null;

    }
}