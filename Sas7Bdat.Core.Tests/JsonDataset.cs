namespace Sas7Bdat.Core.Tests;

/// <summary>
/// Represents a test dataset structure for JSON serialization/deserialization of SAS data and metadata.
/// </summary>
/// <remarks>
/// This record is used in unit tests to compare SAS file parsing results against expected JSON-formatted
/// test data. It provides a structured way to store both the file metadata and the actual data rows
/// in a format that can be easily serialized to/from JSON for test validation purposes.
/// 
/// **Test Data Structure:**
/// The class mirrors the essential components of a SAS dataset:
/// <list type="bullet">
/// <item><description>Metadata: File-level information (structure, encoding, dates, etc.)</description></item>
/// <item><description>Data: The actual observation rows as jagged arrays of objects</description></item>
/// </list>
/// 
/// **Usage in Testing:**
/// This class enables comprehensive validation of SAS file parsing by:
/// <list type="bullet">
/// <item><description>Storing expected results in JSON format alongside test SAS files</description></item>
/// <item><description>Providing exact comparison of parsed metadata against expected values</description></item>
/// <item><description>Enabling row-by-row and cell-by-cell validation of data extraction</description></item>
/// <item><description>Supporting various data types through object? arrays</description></item>
/// </list>
/// 
/// **JSON Compatibility:**
/// The record is designed to work seamlessly with System.Text.Json for:
/// <list type="bullet">
/// <item><description>Serialization of test results to JSON format</description></item>
/// <item><description>Deserialization of expected test data from JSON files</description></item>
/// <item><description>Direct comparison of serialized representations</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Create test dataset for validation
/// var testDataset = new JsonDataset
/// {
///     Metadata = sasFileMetadata,
///     Data = new object?[][]
///     {
///         new object?[] { "John", 25, DateTime.Parse("1998-01-01") },
///         new object?[] { "Jane", 30, DateTime.Parse("1993-05-15") }
///     }
/// };
/// 
/// // Serialize for comparison
/// var json = JsonSerializer.Serialize(testDataset);
/// </code>
/// </example>
public record JsonDataset
{
    /// <summary>
    /// Gets or sets the SAS file metadata containing structural and descriptive information.
    /// </summary>
    /// <value>
    /// A SasFileMetadata instance containing file-level information such as creation date,
    /// encoding, format version, compression type, and dataset structure details.
    /// Defaults to a new empty instance if not specified.
    /// </value>
    /// <remarks>
    /// This property stores the complete metadata extracted from the SAS file header,
    /// including:
    /// <list type="bullet">
    /// <item><description>File format and version information</description></item>
    /// <item><description>Creation and modification timestamps</description></item>
    /// <item><description>Character encoding specifications</description></item>
    /// <item><description>Dataset name and file type</description></item>
    /// <item><description>Row and column count information</description></item>
    /// <item><description>Compression settings and platform details</description></item>
    /// </list>
    /// 
    /// The metadata is used in tests to validate that the SAS file parser correctly
    /// extracts and interprets all header information according to the file format
    /// specifications.
    /// </remarks>
    public SasFileMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the dataset rows as a jagged array of nullable objects.
    /// </summary>
    /// <value>
    /// A two-dimensional jagged array where each outer element represents one row
    /// and each inner element represents the typed values of the columns within that row.
    /// Defaults to an empty array if not specified.
    /// </value>
    /// <remarks>
    /// This property stores the actual data content of the SAS dataset in a format
    /// suitable for JSON serialization and test validation. The structure accommodates:
    /// 
    /// **Data Organization:**
    /// <list type="bullet">
    /// <item><description>Outer array: Each element represents one observation (row)</description></item>
    /// <item><description>Inner arrays: Each element represents one variable (column) value</description></item>
    /// <item><description>Object? type: Supports all .NET types that SAS columns can represent</description></item>
    /// </list>
    /// 
    /// **Supported Data Types:**
    /// <list type="bullet">
    /// <item><description>string: For character/text columns</description></item>
    /// <item><description>double?: For numeric columns</description></item>
    /// <item><description>DateTime?: For date and datetime columns</description></item>
    /// <item><description>TimeSpan?: For time duration columns</description></item>
    /// <item><description>null: For missing values in any column type</description></item>
    /// </list>
    /// 
    /// **Test Validation Usage:**
    /// During testing, this data is compared element-by-element against the results
    /// of parsing actual SAS files to ensure:
    /// <list type="bullet">
    /// <item><description>Correct row count extraction</description></item>
    /// <item><description>Accurate column count per row</description></item>
    /// <item><description>Proper type conversion for each data type</description></item>
    /// <item><description>Correct handling of missing values</description></item>
    /// </list>
    /// 
    /// **Memory Considerations:**
    /// The jagged array structure allows for efficient memory usage while supporting
    /// datasets with varying row structures, though SAS datasets typically have
    /// fixed column layouts.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Example data structure for a 3-column dataset
    /// var data = new object?[][]
    /// {
    ///     new object?[] { "Alice", 28.0, DateTime.Parse("2023-01-15") },
    ///     new object?[] { "Bob", null, DateTime.Parse("2023-02-20") },    // Missing age
    ///     new object?[] { "Charlie", 35.0, null }                        // Missing date
    /// };
    /// 
    /// var dataset = new JsonDataset { Data = data };
    /// </code>
    /// </example>
    public object?[][] Data { get; set; } = [];
}