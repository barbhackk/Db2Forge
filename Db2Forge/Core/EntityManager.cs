using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Db2Forge.Helpers;
using Db2Forge.Infrastructure;
using Db2Forge.Query;

namespace Db2Forge.Core;

public class EntityManager(string config)
{
    private readonly List<string> _pendingQueries = new List<string>();
    internal readonly Dao _dao = new Dao(config);

    public Repository<T> GetRepository<T>() where T : class
    {
        var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>() ?? throw new InvalidOperationException($"Pas d'attribut [Table] sur {typeof(T).Name}");
        return new Repository<T>(tableAttr.Name, this);
    }

    internal void AddPending(string sql)
    {
        _pendingQueries.Add(sql);
    }

    public void Persist(object entity)
    {
        var sql = BuildInsertQuery(entity);
        AddPending(sql);
    }

    public void Flush()
    {
        using var tx = _dao.BeginTransaction();

        try
        {
            foreach (var sql in _pendingQueries)
                _dao.ExecuteNonQuery(sql, tx);

            tx.Commit();
            _pendingQueries.Clear();
        }
        catch (Exception e)
        {
            tx.Rollback();
            _pendingQueries.Clear();
            throw new Exception("Erreur lors de l'exécution", e);
        }

    }

    public void Remove(object entity)
    {
        var sql = BuildDeleteQuery(entity);
        AddPending(sql);
    }

    public void Update(object entity, Criteria? criteria = null)
    {
        var sql = BuildUpdateQuery(entity, criteria);
        AddPending(sql);
    }

    private static string BuildDeleteQuery(object entity)
    {
        var type = entity.GetType();
        var properties = type.GetProperties();
        var tableAttr = type.GetCustomAttribute<TableAttribute>() ?? throw new InvalidOperationException($"Pas d'attribut [Table] sur {entity.GetType().Name}");

        var conditions = new List<string>();

        foreach (var prop in properties)
        {
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
            if (keyAttr == null) continue;

            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;

            var value = prop.GetValue(entity);
            conditions.Add($"{colAttr.Name} = {Formatter.FormatValue(prop, value)}");
        }

        if (conditions.Count == 0)
            throw new InvalidOperationException($"Aucune clé définie sur l'entité {type.Name}");

        return $"DELETE FROM {tableAttr.Name} WHERE {string.Join(" AND ", conditions)}";
    }

    private static string BuildUpdateQuery(object entity, Criteria? criteria = null)
    {
        var type = entity.GetType();
        var properties = type.GetProperties();
        var tableAttr = type.GetCustomAttribute<TableAttribute>() ?? throw new InvalidOperationException($"Pas d'attribut [Table] sur {entity.GetType().Name}");

        var columns = new List<string>();
        var values = new List<string>();
        var conditions = new List<string>();

        foreach (var prop in properties)
        {
            // On récupére la clé
            var keyAttr = prop.GetCustomAttribute<KeyAttribute>();
            // Récupération de la colonne
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;

            var colName = colAttr.Name;
            var value = prop.GetValue(entity);

            if (colName == null)
            {
                throw new Exception("colName is null");
            }

            columns.Add(colName);
            values.Add(Formatter.FormatValue(prop, value));

            if (keyAttr != null && criteria == null)
                conditions.Add($"{colAttr.Name} = {Formatter.FormatValue(prop, value)}");

        }

        if (criteria != null)
        {
            foreach (var entry in criteria.GetEntries())
            {
                var formatted = Formatter.FormatRawValue(entry.Value);
                conditions.Add($"{entry.ColumnName} = {formatted}");
            }
        }

        var setValues = new List<string>();

        // Correction : vérification séparée et logique corrigée
        if (columns.Count == 0)
            throw new InvalidOperationException($"Aucune colonne à mettre à jour sur l'entité {type.Name}");

        if (columns.Count != values.Count)
            throw new InvalidOperationException($"Le nombre de colonnes et de valeurs ne correspond pas");

        if (conditions.Count == 0)
            throw new InvalidOperationException($"Aucune condition définie sur l'entité {type.Name}");

        for (int i = 0; i < columns.Count; i++)
        {
            setValues.Add($"{columns[i]} = {values[i]}");
        }

        return $"UPDATE {tableAttr.Name} SET {string.Join(", ", setValues)} WHERE {string.Join(" AND ", conditions)}";
    }

    private static string BuildInsertQuery(object entity)
    {
        var type = entity.GetType();
        var properties = type.GetProperties();
        var tableAttr = type.GetCustomAttribute<TableAttribute>() ?? throw new InvalidOperationException($"Pas d'attribut [Table] sur {entity.GetType().Name}");

        var columns = new List<string>();
        var values = new List<string>();

        foreach (var prop in properties)
        {
            var colAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (colAttr == null) continue;

            var colName = colAttr.Name;
            var value = prop.GetValue(entity);

            if (colName == null)
            {
                throw new InvalidOperationException($"Pas d'attribut [Column] sur {prop.Name}");
            }

            columns.Add(colName);
            values.Add(Formatter.FormatValue(prop, value));
        }

        return $"INSERT INTO {tableAttr.Name} ({string.Join(", ", columns)}) " +
               $"VALUES ({string.Join(", ", values)})";
    }

    public Dictionary<string, object?> CallProcedure(string procedureName, List<ProcedureParameter> parameters)
    {
        return _dao.CallProcedure(procedureName, parameters);
    }
}
