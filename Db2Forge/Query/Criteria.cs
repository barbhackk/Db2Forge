namespace Db2Forge.Query;

public class Criteria
    {
        private readonly List<CriteriaEntry> _entries = new List<CriteriaEntry>();

        public Criteria Add(string columnName, object value)
        {
            _entries.Add(new CriteriaEntry { ColumnName = columnName, Value = value });
            return this; // fluent
        }

        internal List<CriteriaEntry> GetEntries()
        {
            return _entries;
        }
    }

    internal class CriteriaEntry
    {
        public required string ColumnName { get; set; }
        public required object Value { get; set; }
    }