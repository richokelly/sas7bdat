namespace Sas7Bdat.Core
{
    /// <summary>
    /// Configures options for reading records from SAS data files.
    /// </summary>
    /// <remarks>
    /// This class provides various options to control how data is read from SAS files,
    /// including performance tuning through buffer size configuration, data subset selection
    /// through column filtering, and result limiting through row skip/max options.
    /// </remarks>
    /// <example>
    /// <code>
    /// var options = new RecordReadOptions
    /// {
    ///     FileBufferSize = 65536,
    ///     SkipRows = 100,
    ///     MaxRows = 1000,
    ///     SelectedColumns = new HashSet&lt;string&gt; { "Name", "Age", "Salary" }
    /// };
    /// </code>
    /// </example>
    public sealed class RecordReadOptions
    {
        /// <summary>
        /// Gets or sets the buffer size in bytes used for file I/O operations.
        /// </summary>
        /// <value>
        /// The buffer size in bytes, or null to use the default size.
        /// When null, the system will use max(2 * PageLength, Environment.SystemPageSize).
        /// </value>
        /// <remarks>
        /// Larger buffer sizes can improve performance for sequential reads by reducing
        /// the number of system calls, but use more memory. The optimal size depends on
        /// the file size, available memory, and access patterns.
        /// </remarks>
        public int? FileBufferSize { get; set; }

        /// <summary>
        /// Gets or sets the number of rows to skip from the beginning of the dataset.
        /// </summary>
        /// <value>
        /// The number of rows to skip, or null to skip no rows.
        /// </value>
        /// <remarks>
        /// This is useful for implementing paging scenarios or skipping header rows.
        /// Skipped rows are not returned but are still processed internally, so this
        /// affects performance proportionally to the number of rows skipped.
        /// </remarks>
        public int? SkipRows { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of rows to return.
        /// </summary>
        /// <value>
        /// The maximum number of rows to return, or null to return all available rows.
        /// </value>
        /// <remarks>
        /// This option is applied after SkipRows, so it limits the number of rows
        /// in the result set. Combined with SkipRows, this enables efficient paging
        /// through large datasets.
        /// </remarks>
        public int? MaxRows { get; set; }

        /// <summary>
        /// Gets or sets the set of column names to include in the result.
        /// </summary>
        /// <value>
        /// A HashSet containing the names of columns to include, or null to include all columns.
        /// Column names are case-sensitive and must match the names in the SAS file exactly.
        /// </value>
        /// <remarks>
        /// When both SelectedColumns and SelectedColumnIndices are specified,
        /// SelectedColumnIndices takes precedence. Use this option to reduce memory usage
        /// and improve performance when only a subset of columns is needed.
        /// </remarks>
        public HashSet<string>? SelectedColumns { get; set; }

        /// <summary>
        /// Gets or sets the set of column indices (zero-based) to include in the result.
        /// </summary>
        /// <value>
        /// A HashSet containing the zero-based indices of columns to include, or null to include all columns.
        /// </value>
        /// <remarks>
        /// This option takes precedence over SelectedColumns when both are specified.
        /// Column indices are zero-based and must be valid for the dataset being read.
        /// Use this option when you know the column positions but not necessarily the names.
        /// </remarks>
        public HashSet<int>? SelectedColumnIndices { get; set; }

        /// <summary>
        /// Gets the set of column indices to be included based on the current selection options.
        /// </summary>
        /// <param name="columns">The column metadata from the SAS file.</param>
        /// <returns>
        /// A HashSet containing the zero-based indices of columns to include in the result.
        /// If no columns are specifically selected, returns indices for all columns.
        /// </returns>
        /// <remarks>
        /// This method processes the selection criteria in the following priority order:
        /// 1. If SelectedColumnIndices is set, those indices are used directly
        /// 2. If SelectedColumns is set, column names are matched to find corresponding indices
        /// 3. If neither is set, all column indices are returned
        /// 
        /// Invalid column names in SelectedColumns are silently ignored.
        /// </remarks>
        /// <exception cref="ArgumentException">
        /// May be thrown if SelectedColumnIndices contains indices that are out of range
        /// for the provided columns array.
        /// </exception>
        internal HashSet<int> GetColumnIndices(ReadOnlyMemory<SasColumnInfo> columns)
        {
            if (SelectedColumnIndices != null)
                return [.. SelectedColumnIndices];

            if (SelectedColumns is not { Count: > 0 }) return [.. Enumerable.Range(0, columns.Length)];

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

        /// <summary>
        /// Determines whether all columns should be selected based on the current options.
        /// </summary>
        /// <returns>
        /// true if all columns should be selected (no specific column selection is configured);
        /// false if specific columns have been selected.
        /// </returns>
        /// <remarks>
        /// This method returns true when both SelectedColumns and SelectedColumnIndices
        /// are either null or empty, indicating that no specific column filtering has been requested.
        /// This information is used internally to optimize the data reading process.
        /// </remarks>
        internal bool ShouldSelectAllColumns()
        {
            return (SelectedColumns == null || SelectedColumns.Count == 0) &&
                   (SelectedColumnIndices == null || SelectedColumnIndices.Count == 0);
        }
    }
}