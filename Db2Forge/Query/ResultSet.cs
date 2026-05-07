using System.Data;
using Db2Forge.Helpers;

namespace Db2Forge.Query;

public class ResultSet<T>(DataSet data) where T : class
{
    private readonly DataSet _data = data;

    public List<T> FetchAllAssociative()
    {
        return Mapper.MapDataSet<T>(_data);
    }

    public DataSet GetResult()
    {
        return _data;
    }

    /// <summary>
    /// Retourne le resultat du type attendu.
    /// <para>Une seule ligne du dataset est remontée</para>
    /// </summary>
    /// <returns></returns>
    public T? FetchAssociative()
    {
        return Mapper.MapOneDataSet<T>(_data);
    }

    /// <summary>
    /// Retourne le nombre de lignes dans le dataset
    /// </summary>
    /// <returns></returns>
    public int GetCount()
    {
        return _data.Tables[0].Rows.Count;
    }
}