using System.Data;

namespace ORM.For.Db2;

public class ProcedureParameter
{
    public string Name { get; set; }
    public Type Type { get; set; }
    public object Value { get; set; }
    public int Size { get; set; }
    public ParameterDirection Direction { get; set; }

    public ProcedureParameter(string name, Type type, object value, int size, ParameterDirection direction = ParameterDirection.Input)
    {
        Name = name;
        Type = type;
        Value = value;
        Size = size;
        Direction = direction;
    }
}
