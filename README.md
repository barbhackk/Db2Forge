<p align="center">
  <img src="https://github.com/barbhackk/Db2Forge/blob/main/db2forge.png?raw=true" width="300" alt="Db2Forge Icon">
</p>


> A Doctrine-inspired ORM built for DB2/AS400 and .NET Framework 4.6.2+ et .NET 8+. No magic, no bloat — just clean EntityManager, Repository, and Flush-based transactions over ODBC.

<p align="center">
<img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT">
<img src="https://img.shields.io/badge/.NET-4.6.2+-orange.svg" alt=".NET Framework">
<img src="https://img.shields.io/badge/.NET-8%2B-blue.svg" alt=".NET">
<img src="https://img.shields.io/github/stars/barbhackk/Db2Forge" alt="stars">
</p>

Package is available on [NuGet](https://www.nuget.org/packages/Db2Forge)

---

## Features

- `EntityManager` — central entry point, inspired by Doctrine
- `Repository<T>` — typed queries with `FindAll`, `FindBy`, `FindOneBy`
- `Criteria` — fluent filter builder
- **Unit of Work** — `Persist` / `Remove` / `Update` + `Flush` in a single transaction
- `CallProcedure` — stored procedure support with `Input` / `Output` parameters
- Reflection-based mapping via standard .NET attributes (`[Table]`, `[Column]`, `[Key]`)

---

## Why ODBC ?

Db2Forge uses the **IBM i Access ODBC Driver** instead of DB2 Connect.

This means:
- ✅ No IBM DB2 Connect license required (saves hundreds/thousands €)
- ✅ Driver is free and included with IBM i Access Client Solutions
- ✅ Already installed in most AS/400 environments

---

## Requirements

- .NET Framework 4.6+ and .NET 8+
- IBM i Access ODBC Driver installed on the host machine
- DB2/AS400 accessible via ODBC

---

## Quick Start

### 1. Define your entity

Map your AS400 table using standard .NET Data Annotations:

```csharp
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("MYBIB")]
public class MyClass
{
    [Key]
    [Column("FOCMUT")]
    public required string CodeMutation { get; set; }

    [Column("FOORGF")]
    public required string DistManagementState { get; set; }

    [Column("FOCFOR")]
    public required string StatusExplanationCode { get; set; }

    [Column("FONOM")]
    public required string SapNumber { get; set; }

    [Column("FOETAT")]
    public required string NetworkCode { get; set; }

    [Column("DNAIDT")]
    public required DateTime CreationDate { get; set; }

    [Column("DNAQTX")]
    public required string CreationProfile { get; set; }
}
```

### 2. Initialize the EntityManager

In configuration file (ex: web.config):

```xml
<configuration>
	<connectionStrings>
		...		
	</connectionStrings>
	<appSettings>
		<add key="webpages:Version" value="3.0.0.0" />
		<add key="webpages:Enabled" value="false" />

		<add key="Db2ForgeConnectionString" value="Driver={IBM i Access ODBC Driver};System=MLPDEV;Uid=SIR2000;Pwd=SIR2000;DefaultLibraries=SIREDG;" />

		<!-- Code de l'application -->
		<add key="CodeApplication" value="SIR" />
```

```csharp
var manager = new EntityManager(ConfigurationManager.AppSettings["Db2ForgeConnectionString"]);
```

It is better to specify the connection string in the settings or an environment variable than to pass it directly as a parameter.

---

## Usage

### Get a Repository

```csharp
var repo = manager.GetRepository<MyClass>();
```

### Find all records

```csharp
var entities = repo.FindAll();
Console.WriteLine(entities.Count);
```

### Find with criteria (fluent)

```csharp
var criteria = new Criteria().Add("FOCMUT", "D");
var result = repo.FindOneBy(criteria);
```

Multiple conditions:

```csharp
var criteria = new Criteria()
    .Add("FOORGF", "01")
    .Add("FOCFOR", "SUCCESS");

var results = repo.FindBy(criteria);
```

### Insert

```csharp
var d = new MyClass
{
    CodeMutation          = "D",
    DistManagementState   = "01",
    StatusExplanationCode = "SUCCESS",
    SapNumber             = "06546546",
    NetworkCode           = "545",
    CreationDate          = DateTime.Now,
    CreationProfile       = "SEBASTIEN"
};

manager.Persist(d);
manager.Flush();
```

### Update

```csharp
var criteria = new Criteria().Add("FOORGF", "01");
var entity = repo.FindOneBy(criteria);

if (entity != null)
{
    entity.CreationProfile = "DOUTRE";
    
    // Using de [Key] property on entity
    manager.Update(entity);
    // Or if you want specify other conditions 
    manager.Update(distributor, criteria);

    manager.Flush();
}
```

### Delete

```csharp

// Using [Key] property on your entity
manager.Remove(entity);
manager.Flush();
```

### Batch operations (Unit of Work)

Operations are queued and executed in a **single atomic transaction** on `Flush()`:

```csharp
manager.Persist(entityA);
manager.Persist(entityB);
manager.Remove(entityC);
manager.Flush(); // all or nothing
```

---

## Stored Procedures

Call AS400 stored procedures with typed `Input` / `Output` parameters:

```csharp
var output = manager.CallProcedure("SPLIBPROC", new List<ProcedureParameter>
{
    new("CODEMUT",typeof(string), "D",                    7),
    new("DATEFF", typeof(string), DateTime.Now.ToString("yyyyMMdd"), 8),
    new("QUALIF", typeof(string), "", 1,  ParameterDirection.Output),
    new("CODE",   typeof(string), "", 1,  ParameterDirection.Output),
    new("LIBEL",  typeof(string), "", 30, ParameterDirection.Output),
    new("PRET",   typeof(string), "", 2,  ParameterDirection.Output),
});

Console.WriteLine(output["LIBEL"]?.ToString());
```

`CallProcedure` returns a `Dictionary<string, object?>` containing all `Output` and `InputOutput` parameter values.

## Execute custom SQL

Execute custom SQL statements with

```csharp
var entityManager = new EntityManager(ConfigurationManager.AppSettings["Db2ForgeConnectionString"]);

StringBuilder strSQL = new StringBuilder();
List<string> whereSql = new List<string>();

strSQL.AppendLine("SELECT * FROM MYBIB");
strSQL.AppendLine("LEFT JOIN YDICMP ON CDPQCD = MYBIB.FOCMUT");

try
{
    whereSql.Add("MYBIB.FOCMUT = '" + param + "'");

    if (whereSql.Count > 0)
    {
        strSQL.AppendLine("WHERE " + string.Join(" AND ", whereSql));
    }

    var ressultSet = entityManager.ExecuteQuery<DiffuserDetailModel>(strSQL.ToString());
    
    return ressultSet.FetchAssociative();
}
catch (System.Exception ex)
{
    throw ex;
}
```

The ResultSet is typed and allows retrieving data as a list of objects of the specified type with `FetchAllAssociative()` or a single object with `FetchAssociative()`.
It is also possible to get the number of entities found with `GetCount()`.
`GetResult()` allows accessing the raw dataset.

---

## Entity Mapping Reference

| Attribute | Target | Role |
|-----------|--------|------|
| `[Table("TABLENAME")]` | Class | Maps to the AS400 physical file |
| `[Column("COLNAME")]` | Property | Maps to the AS400 column name |
| `[Key]` | Property | Marks the primary key (used in `WHERE` for Update/Delete) |

---

## Project Structure

```
Db2Forge/
├── Core/
    ├── EntityManager.cs       # Entry point — Persist, Flush, Update, Remove, CallProcedure
    ├── Repository.cs          # FindAll, FindBy, FindOneBy
├── Helpers/
    ├── Formatter.cs           # SQL value formatting
    ├── Mapper.cs              # DataSet → typed object mapping
├── Infrastructure
    ├── Dao.cs                 # Raw ODBC layer — Fetch, ExecuteNonQuery, CallProcedure
    ├── ProcedureParameter.cs  # Input/Output parameter descriptor
└── Query/
    ├── Criteria.cs            # Fluent filter builder
    └── ResultSet.cs           # ResultSet for custom query
```

---

Sébastien Doutre

## License

MIT — see [LICENSE](LICENSE) for details.