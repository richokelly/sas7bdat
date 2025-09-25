namespace Sas7Bdat.Core
{
    public sealed class RecordReadOptions
    {
        public int? SkipRows { get; set; }
        public int? MaxRows { get; set; }
        public HashSet<string>? SelectedColumns { get; set; }
        public HashSet<int>? SelectedColumnIndices { get; set; }

        internal HashSet<int> GetColumnIndices(ReadOnlyMemory<SasColumnInfo> columns)
        {
            if (SelectedColumnIndices != null)
                return [..SelectedColumnIndices];

            if (SelectedColumns is not { Count: > 0 }) return [..Enumerable.Range(0, columns.Length)];
            
            var indices = new HashSet<int>();
            for (var i = 0; i < columns.Length; i++)
            {
                if (SelectedColumns.Contains(columns.Span[i].Name))
                {
                    indices.Add(i);
                }
            }
         
            return indices;
        }

        internal bool ShouldSelectAllColumns()
        {
            return (SelectedColumns == null || SelectedColumns.Count == 0) &&
                   (SelectedColumnIndices == null || SelectedColumnIndices.Count == 0);
        }
    }
}