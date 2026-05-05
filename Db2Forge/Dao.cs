namespace ORM;

using System.Data.Odbc;
using System.Data;
using ORM.For.Db2;

public class Dao(string connectionString)
{
    private readonly string _connectionString = connectionString;
    private OdbcConnection CreateConnection() => new(_connectionString);

    public OdbcTransaction BeginTransaction()
    {
        var conn = CreateConnection();
        conn.Open();
        return conn.BeginTransaction();
    }

    public DataSet Fetch(string sql)
    {
        using var conn = CreateConnection();
        conn.Open();

        using var cmd = new OdbcCommand(sql, conn);
        using var adapter = new OdbcDataAdapter(cmd);

        DataSet dataSet = new DataSet();
        adapter.Fill(dataSet);

        return dataSet;
    }

    public void ExecuteNonQuery(string sql, OdbcTransaction? transaction = null)
    {
        // si transaction fournie, on utilise sa connexion
        var conn = transaction?.Connection ?? CreateConnection();
        var ownsConnection = transaction == null;

        if (ownsConnection) conn.Open();

        using var cmd = new OdbcCommand(sql, conn);
        cmd.Transaction = transaction;

        if (ownsConnection)
        {
            // auto-transaction interne
            using var tx = conn.BeginTransaction();
            cmd.Transaction = tx;
            try
            {
                cmd.ExecuteNonQuery();
                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
            finally
            {
                conn.Dispose();
            }
        }
        else
        {
            // la transaction est gérée par l'appelant
            cmd.ExecuteNonQuery();
        }
    }

    public Dictionary<string, object?> CallProcedure(string procedureName, List<ProcedureParameter> parameters)
    {
        using var conn = new OdbcConnection(_connectionString);
        conn.Open();

        // ODBC appelle les procédures AS/400 avec la syntaxe {call MYLIB.MAPROC(?,?)}
        var placeholders = string.Join(", ", parameters.Select(_ => "?"));
        var sql = $"{{call {procedureName}({placeholders})}}";

        using var cmd = new OdbcCommand(sql, conn);
        cmd.CommandType = CommandType.StoredProcedure;

        foreach (var p in parameters)
        {
            var odbcParam = new OdbcParameter(p.Name, p.Type)
            {
                Direction = p.Direction,
                Value = p.Value ?? DBNull.Value
            };

            if (p.Size > 0)
                odbcParam.Size = p.Size;

            cmd.Parameters.Add(odbcParam);
        }

        cmd.ExecuteNonQuery();

        // on retourne les valeurs Output/InputOutput
        return cmd.Parameters
            .Cast<OdbcParameter>()
            .Where(p => p.Direction is ParameterDirection.Output or ParameterDirection.InputOutput)
            .ToDictionary(
                p => p.ParameterName,
                p => p.Value is DBNull ? null : p.Value
            );
    }
}