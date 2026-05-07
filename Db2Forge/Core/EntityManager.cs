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

    /// <summary>
    /// Retourne un repository pour une entité donnée
    /// Le repository permet de faire des opérations CRUD sur l'entité
    /// Le nom de la table est récupéré depuis l'attribut [Table] sur l'entité
    /// Si l'attribut n'est pas présent, une exception est levée
    /// Le repository est créé à chaque appel, il n'est pas mis en cache
    /// </summary>
    /// <typeparam name="T">Type de l'entité</typeparam>
    /// <returns>Repository pour l'entité</returns>
    /// <exception cref="InvalidOperationException"></exception>
    public Repository<T> GetRepository<T>() where T : class
    {
        var tableAttr = typeof(T).GetCustomAttribute<TableAttribute>() ?? throw new InvalidOperationException($"Pas d'attribut [Table] sur {typeof(T).Name}");
        return new Repository<T>(tableAttr.Name, this);
    }

    /// <summary>
    /// Ajoute une requête SQL à la liste des requêtes en attente de persistance
    /// </summary>
    /// <param name="sql">Requête SQL à ajouter</param>
    internal void AddPending(string sql)
    {
        _pendingQueries.Add(sql);
    }

    /// <summary>
    /// Persiste une entité en base de données
    /// </summary>
    /// <param name="entity">Entité à persister</param>
    public void Persist(object entity)
    {
        var sql = BuildInsertQuery(entity);
        AddPending(sql);
    }

    /// <summary>
    /// Exécute toutes les requêtes en attente de persistance
    /// </summary>
    /// <exception cref="Exception">Erreur lors de l'exécution des requêtes</exception>
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

    /// <summary>
    /// Supprime une entité de la base de données
    /// </summary>
    /// <param name="entity">Entité à supprimer</param>
    public void Remove(object entity)
    {
        var sql = BuildDeleteQuery(entity);
        AddPending(sql);
    }

    /// <summary>
    /// Met à jour une entité dans la base de données
    /// Si le critère est null, on utilise la clé primaire de l'entité 
    /// Si le critère est fourni, on utilise les conditions du critère pour identifier l'entité à mettre à jour
    /// Si le critère est fourni et que l'entité n'existe pas, une exception est levée
    /// </summary>
    /// <param name="entity">Entité à mettre à jour</param>
    /// <param name="criteria">Critère de mise à jour (peut être null)</param>
    public void Update(object entity, Criteria? criteria = null)
    {
        var sql = BuildUpdateQuery(entity, criteria);
        AddPending(sql);
    }

    /// <summary>
    /// Execute la requête SQL et retourne un objet de type ResultSet
    /// </summary>
    /// <typeparam name="T">Type de l'entité</typeparam>
    /// <param name="sql">Requête SQL</param>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public ResultSet<T> ExecuteQuery<T>(string sql) where T : class
    {
        try
        {
            var result = _dao.Fetch(sql);            
            return new ResultSet<T>(result);
        }
        catch (Exception e)
        {
            throw new Exception("Erreur lors de l'exécution", e);
        }        
    }

    /// <summary>
    /// Construit la requête SQL pour supprimer une entité
    /// </summary>
    /// <param name="entity">Entité à supprimer</param>
    /// <returns>Requête SQL</returns>
    /// <exception cref="InvalidOperationException"></exception>
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

    /// <summary>
    /// Construit une requête UPDATE pour une entité donnée.
    /// </summary>
    /// <param name="entity">Entité à mettre à jour</param>
    /// <param name="criteria">Critères de mise à jour</param>
    /// <returns>Requête UPDATE</returns>
    /// <exception cref="InvalidOperationException">Si l'entité n'a pas d'attribut [Table]</exception>
    /// <exception cref="Exception">Si l'entité n'a pas de clé définie</exception>
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

    /// <summary>
    /// Construit une requête SQL INSERT pour une entité donnée.
    /// </summary>
    /// <param name="entity">L'entité à insérer.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
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

    /// <summary>
    /// Appelle une procédure stockée avec les paramètres fournis.
    /// </summary>
    /// <param name="procedureName">Nom de la procédure stockée</param>
    /// <param name="parameters">Liste des paramètres de la procédure</param>
    /// <returns>Dictionnaire contenant les résultats de la procédure</returns>
    public Dictionary<string, object?> CallProcedure(string procedureName, List<ProcedureParameter> parameters)
    {
        return _dao.CallProcedure(procedureName, parameters);
    }
}
