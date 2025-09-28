namespace Sas7Bdat.Core.Serializers;

/// <summary>
/// Provides selective column serialization for processing only specified columns from row data.
/// </summary>
/// <param name="columns">The complete column metadata for the dataset.</param>
/// <param name="columnIndices">The set of zero-based column indices to include in the output.</param>
/// <remarks>
/// SubsetOfColumnsSerializer extends ColumnSerializer to support scenarios where only
/// a subset of columns needs to be processed, providing significant performance and
/// memory benefits when working with wide datasets where only specific columns are needed.
/// 
/// **Key Benefits:**
/// <list type="bullet">
/// <item><description>Reduced processing overhead by skipping unwanted columns</description></item>
/// <item><description>Lower memory usage with smaller output arrays</description></item>
/// <item><description>Improved cache performance with better data locality</description></item>
/// <item><description>Faster data transfer and serialization of results</description></item>
/// </list>
/// 
/// **Use Cases:**
/// <list type="bullet">
/// <item><description>Data analysis workflows requiring only specific columns</description></item>
/// <item><description>ETL processes with column filtering requirements</description></item>
/// <item><description>Memory-constrained environments with large datasets</description></item>
/// <item><description>Network transfers where bandwidth optimization is important</description></item>
/// </list>
/// 
/// **Column Selection Strategy:**
/// The class processes all columns in their natural order but only extracts and converts
/// those specified in the columnIndices set. This approach maintains the binary parsing
/// correctness while providing selective output generation.
/// 
/// **Output Organization:**
/// Selected columns are placed in the output array in the order they appear in the
/// original dataset, not in the order they appear in the columnIndices set. This
/// provides predictable and consistent output organization.
/// </remarks>
/// <example>
/// <code>
/// // Select only columns 0, 2, and 5 from a 10-column dataset
/// var selectedIndices = new HashSet&lt;int&gt; { 0, 2, 5 };
/// var serializer = new SubsetOfColumnsSerializer(columns, selectedIndices);
/// 
/// // Output array only needs space for 3 values instead of 10
/// var outputValues = new object?[3];
/// serializer.Deserialize(rowData, outputValues);
/// 
/// // Values are in order: [column0_value, column2_value, column5_value]
/// </code>
/// </example>
internal class SubsetOfColumnsSerializer(ReadOnlyMemory<SasColumnInfo> columns, HashSet<int> columnIndices) : ColumnSerializer(columns)
{
    /// <summary>
    /// Deserializes only the selected columns from binary row data into the destination array.
    /// </summary>
    /// <param name="rowData">The complete binary data representing one row from the SAS file.</param>
    /// <param name="destination">The output array where selected column values will be stored.</param>
    /// <remarks>
    /// This override provides selective column processing by iterating through all columns
    /// but only extracting and converting those specified in the columnIndices set. The
    /// implementation maintains efficiency while providing the flexibility of column selection.
    /// 
    /// **Processing Algorithm:**
    /// <list type="number">
    /// <item><description>Initialize a separate index counter for the output array</description></item>
    /// <item><description>Iterate through all columns in their natural order</description></item>
    /// <item><description>For each column, check if its index is in the selection set</description></item>
    /// <item><description>If selected, extract and convert the column data</description></item>
    /// <item><description>Store the converted value at the current output position</description></item>
    /// <item><description>Increment the output position only for selected columns</description></item>
    /// </list>
    /// 
    /// **Performance Characteristics:**
    /// <list type="bullet">
    /// <item><description>Time complexity: O(n) where n is the total number of columns</description></item>
    /// <item><description>Space complexity: O(k) where k is the number of selected columns</description></item>
    /// <item><description>Processing overhead: Only for selected columns</description></item>
    /// <item><description>Memory access: Sequential for optimal cache performance</description></item>
    /// </list>
    /// 
    /// **Input Requirements:**
    /// <list type="bullet">
    /// <item><description>rowData must contain complete binary data for all columns</description></item>
    /// <item><description>destination must have at least as many elements as selected columns</description></item>
    /// <item><description>columnIndices must contain valid indices within the column range</description></item>
    /// </list>
    /// 
    /// **Output Guarantees:**
    /// <list type="bullet">
    /// <item><description>Selected columns appear in their natural dataset order</description></item>
    /// <item><description>No gaps in the output array - values are packed sequentially</description></item>
    /// <item><description>Type conversion follows the same rules as the base ColumnSerializer</description></item>
    /// </list>
    /// 
    /// **Thread Safety:**
    /// This method is thread-safe as long as the columnIndices set is not modified
    /// during execution. Multiple threads can safely call this method concurrently
    /// with different row data and destination arrays.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example with a 5-column dataset selecting columns 1 and 3
    /// var selectedColumns = new HashSet&lt;int&gt; { 1, 3 };
    /// var serializer = new SubsetOfColumnsSerializer(allColumns, selectedColumns);
    /// 
    /// var rowData = ReadBinaryRowData(); // Contains data for all 5 columns
    /// var selectedValues = new object?[2]; // Only need space for 2 selected columns
    /// 
    /// serializer.Deserialize(rowData, selectedValues);
    /// 
    /// // selectedValues[0] contains the value from column 1
    /// // selectedValues[1] contains the value from column 3
    /// // Columns 0, 2, and 4 are skipped entirely
    /// 
    /// // Access the selected values
    /// var column1Value = selectedValues[0]; // From original column 1
    /// var column3Value = selectedValues[1]; // From original column 3
    /// </code>
    /// </example>
    public override void Deserialize(ReadOnlySpan<byte> rowData, Span<object?> destination)
    {
        var valueIndex = 0;
        var span = Columns.Span;
        for (var i = 0; i < span.Length; i++)
        {
            if (!columnIndices.Contains(i)) continue;
            destination[valueIndex++] = ExtractValue(rowData, span[i]);
        }
    }
}