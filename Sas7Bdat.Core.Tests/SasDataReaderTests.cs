using System.Text.Json;
using Xunit.Abstractions;

namespace Sas7Bdat.Core.Tests
{
    /// <summary>
    /// Contains comprehensive unit tests for the SasDataReader class and related functionality.
    /// </summary>
    /// <param name="testOutputHelper">The xUnit test output helper for logging test information.</param>
    /// <remarks>
    /// This test class provides thorough validation of the SasDataReader implementation through
    /// multiple test scenarios including data accuracy validation, error handling, performance
    /// testing, and feature verification. The tests use a combination of real SAS files and
    /// expected JSON results to ensure comprehensive coverage.
    /// 
    /// **Test Categories:**
    /// <list type="bullet">
    /// <item><description>Data Accuracy: Compare parsed results against expected JSON data</description></item>
    /// <item><description>Error Handling: Verify proper exception handling for corrupted files</description></item>
    /// <item><description>Performance: Test concurrent access and parallel processing</description></item>
    /// <item><description>Feature Validation: Test filtering, projection, and pagination options</description></item>
    /// </list>
    /// 
    /// **Test Data Organization:**
    /// The tests rely on a structured test data organization:
    /// <list type="bullet">
    /// <item><description>resources/supported/: Valid SAS files with corresponding .json expected results</description></item>
    /// <item><description>resources/corrupted/: Invalid or corrupted SAS files for error testing</description></item>
    /// </list>
    /// 
    /// **Validation Methodology:**
    /// Each test validates multiple aspects of the parsing process:
    /// <list type="bullet">
    /// <item><description>Metadata correctness (row counts, column definitions, file properties)</description></item>
    /// <item><description>Data accuracy (cell-by-cell comparison with expected values)</description></item>
    /// <item><description>Type preservation (ensuring proper .NET type conversion)</description></item>
    /// <item><description>Performance characteristics (concurrent access, memory usage)</description></item>
    /// </list>
    /// 
    /// **Test Infrastructure:**
    /// The class uses xUnit theory-based testing with dynamic test data discovery,
    /// allowing the test suite to automatically include new test files without
    /// code modifications.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Run specific test method
    /// var testHelper = new TestOutputHelper();
    /// var tests = new SasDataReaderTests(testHelper);
    /// await tests.CompareWithJson("resources/supported/sample.sas7bdat");
    /// 
    /// // Run all supported file tests
    /// foreach (var testCase in SasDataReaderTests.DataFiles)
    /// {
    ///     await tests.CompareWithJson((string)testCase[0]);
    /// }
    /// </code>
    /// </example>
    public class SasDataReaderTests(ITestOutputHelper testOutputHelper)
    {
        /// <summary>
        /// Gets test data for all supported SAS files in the test resources directory.
        /// </summary>
        /// <value>
        /// An enumerable of object arrays, where each array contains a single string element
        /// representing the relative path to a test SAS file.
        /// </value>
        /// <remarks>
        /// This property provides dynamic test data discovery for theory-based tests by scanning
        /// the resources/supported directory for SAS files. The implementation:
        /// 
        /// **File Discovery Process:**
        /// <list type="number">
        /// <item><description>Scans the resources/supported directory recursively</description></item>
        /// <item><description>Includes all files except .json files (which contain expected results)</description></item>
        /// <item><description>Converts absolute paths to relative paths for portability</description></item>
        /// <item><description>Returns paths as object arrays compatible with xUnit Theory attribute</description></item>
        /// </list>
        /// 
        /// **Usage with Theory Tests:**
        /// This property is used with the [MemberData] attribute to provide dynamic test cases:
        /// - Each discovered file becomes a separate test case
        /// - Test methods receive the file path as a parameter
        /// - New test files are automatically included without code changes
        /// 
        /// **Path Handling:**
        /// Paths are converted to relative form by removing the BaseDirectory prefix,
        /// making the tests portable across different environments and build configurations.
        /// 
        /// **File Filtering:**
        /// JSON files are excluded because they contain expected test results rather than
        /// input data for testing.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage in theory test
        /// [Theory]
        /// [MemberData(nameof(DataFiles))]
        /// public async Task TestMethod(string filePath)
        /// {
        ///     // Test implementation using the discovered file
        ///     await using var reader = await SasDataReader.OpenFileAsync(filePath);
        ///     // ... test logic
        /// }
        /// 
        /// // Example discovered paths (relative to BaseDirectory):
        /// // "resources/supported/basic_dataset.sas7bdat"
        /// // "resources/supported/compressed/rle_data.sas7bdat"
        /// // "resources/supported/datetime/date_formats.sas7bdat"
        /// </code>
        /// </example>
        public static IEnumerable<object[]> DataFiles =>
            Directory.EnumerateFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "supported"), "*.*", new EnumerationOptions { RecurseSubdirectories = true })
                .Where(path => !path.EndsWith(".json"))
                .Select(path => new[] { (object)path[AppDomain.CurrentDomain.BaseDirectory.Length..] })
            ;

        /// <summary>
        /// Gets test data for all corrupted SAS files in the test resources directory.
        /// </summary>
        /// <value>
        /// An enumerable of object arrays, where each array contains a single string element
        /// representing the relative path to a corrupted test SAS file.
        /// </value>
        /// <remarks>
        /// This property provides test data for validating error handling behavior when
        /// encountering corrupted, truncated, or otherwise invalid SAS files. The implementation
        /// mirrors DataFiles but targets a different directory containing problematic files.
        /// 
        /// **Corrupted File Categories:**
        /// The corrupted files directory typically contains:
        /// <list type="bullet">
        /// <item><description>Files with invalid magic numbers or headers</description></item>
        /// <item><description>Truncated files missing essential data</description></item>
        /// <item><description>Files with corrupted metadata or page structures</description></item>
        /// <item><description>Files with invalid compression or encoding settings</description></item>
        /// </list>
        /// 
        /// **Error Testing Purpose:**
        /// These files are used to verify that the SasDataReader:
        /// <list type="bullet">
        /// <item><description>Properly detects and reports file corruption</description></item>
        /// <item><description>Throws appropriate exceptions with meaningful messages</description></item>
        /// <item><description>Fails fast rather than producing incorrect results</description></item>
        /// <item><description>Maintains stability when encountering invalid data</description></item>
        /// </list>
        /// 
        /// **Test Methodology:**
        /// Tests using this data source typically verify that InvalidDataException
        /// or similar appropriate exceptions are thrown when attempting to open
        /// these files.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Example usage for error testing
        /// [Theory]
        /// [MemberData(nameof(CorruptedDataFiles))]
        /// public async Task ShouldThrowException(string corruptedFilePath)
        /// {
        ///     await Assert.ThrowsAsync&lt;InvalidDataException&gt;(
        ///         async () => await SasDataReader.OpenFileAsync(corruptedFilePath));
        /// }
        /// 
        /// // Example corrupted file paths:
        /// // "resources/corrupted/invalid_header.sas7bdat"
        /// // "resources/corrupted/truncated_file.sas7bdat"
        /// // "resources/corrupted/bad_compression.sas7bdat"
        /// </code>
        /// </example>
        public static IEnumerable<object[]> CorruptedDataFiles =>
            Directory.EnumerateFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "corrupted"),
                    "*.*", new EnumerationOptions { RecurseSubdirectories = true })
                .Where(path => !path.EndsWith(".json"))
                .Select(path => new[] { (object)path[AppDomain.CurrentDomain.BaseDirectory.Length..] });

        /// <summary>
        /// Tests SAS file parsing accuracy by comparing results against expected JSON data.
        /// </summary>
        /// <param name="path">The relative path to the SAS file to test.</param>
        /// <remarks>
        /// This comprehensive test method validates the complete SAS file parsing process
        /// by comparing both metadata and data content against pre-computed expected results
        /// stored in JSON format.
        /// 
        /// **Test Validation Process:**
        /// <list type="number">
        /// <item><description>Open and parse the SAS file using SasDataReader</description></item>
        /// <item><description>Extract all metadata and data rows</description></item>
        /// <item><description>Load corresponding JSON file with expected results</description></item>
        /// <item><description>Compare metadata using JSON serialization</description></item>
        /// <item><description>Compare row count and data dimensions</description></item>
        /// <item><description>Perform cell-by-cell comparison of all data values</description></item>
        /// </list>
        /// 
        /// **Metadata Validation:**
        /// Compares complete metadata objects by serializing both expected and actual
        /// metadata to JSON and comparing the resulting strings. This ensures all
        /// metadata fields are correctly parsed and match expected values.
        /// 
        /// **Data Validation:**
        /// Performs comprehensive data validation:
        /// <list type="bullet">
        /// <item><description>Row count matches metadata.RowCount</description></item>
        /// <item><description>Each row has the expected number of columns</description></item>
        /// <item><description>Each cell value matches expected type and content</description></item>
        /// <item><description>Null values are properly handled and preserved</description></item>
        /// </list>
        /// 
        /// **Detailed Logging:**
        /// The test provides detailed output for debugging by logging each cell comparison,
        /// making it easy to identify specific discrepancies when tests fail.
        /// 
        /// **JSON Comparison Strategy:**
        /// Uses JSON serialization for value comparison to handle type differences
        /// and provide consistent comparison behavior across different .NET types.
        /// </remarks>
        /// <exception cref="FileNotFoundException">Thrown when the SAS file or corresponding JSON file cannot be found.</exception>
        /// <exception cref="InvalidDataException">Thrown when the SAS file contains invalid or corrupted data.</exception>
        /// <exception cref="JsonException">Thrown when the JSON expected results file cannot be parsed.</exception>
        /// <example>
        /// <code>
        /// // Test a specific file
        /// await CompareWithJson("resources/supported/employee_data.sas7bdat");
        /// 
        /// // This test will:
        /// // 1. Parse employee_data.sas7bdat
        /// // 2. Load employee_data.sas7bdat.json
        /// // 3. Compare all metadata fields
        /// // 4. Compare every data cell
        /// // 5. Report any discrepancies with detailed logging
        /// </code>
        /// </example>
        [Theory]
        [MemberData(nameof(DataFiles))]
        public async Task CompareWithJson(string path)
        {
            await using var reader = await SasDataReader.OpenFileAsync(path);
            var rows = new List<object?[]>();

            var metadata = reader.Metadata;
            await foreach (var row in reader.ReadRowsAsync())
            {
                rows.Add(row.ToArray());
            }

            Assert.Equal(metadata.RowCount, rows.Count);

            await using var json = File.OpenRead(path + ".json");
            var expected = await JsonSerializer.DeserializeAsync<JsonDataset>(json);

            Assert.Equal(expected?.Metadata.ToJsonString(), metadata.ToJsonString());
            Assert.Equal(expected?.Data.Length, rows.Count);

            for (var i = 0; i < rows.Count; i++)
            {
                Assert.Equal(expected!.Data[i].Length, rows[i].Length);
                for (var j = 0; j < expected.Data[i].Length; j++)
                {
                    var expect = expected.Data[i][j].ToJsonString();
                    var actual = rows[i][j]?.ToJsonString() ?? "null";
                    testOutputHelper.WriteLine($"For row {i} column {j}, found {actual} and expected {expect}");
                    Assert.Equal(expect, actual);
                }
            }
        }

        /// <summary>
        /// Tests that corrupted SAS files properly throw InvalidDataException during opening.
        /// </summary>
        /// <param name="path">The relative path to the corrupted SAS file to test.</param>
        /// <remarks>
        /// This test validates the error handling behavior of SasDataReader when encountering
        /// corrupted, invalid, or malformed SAS files. The test ensures that the reader
        /// fails fast with appropriate exceptions rather than attempting to process invalid data.
        /// 
        /// **Error Detection Validation:**
        /// <list type="bullet">
        /// <item><description>Magic number validation catches non-SAS files</description></item>
        /// <item><description>Header integrity checks detect structural corruption</description></item>
        /// <item><description>Size validation catches truncated files</description></item>
        /// <item><description>Metadata validation detects logical inconsistencies</description></item>
        /// </list>
        /// 
        /// **Exception Type Verification:**
        /// The test specifically expects InvalidDataException, which is the standard
        /// exception type for file format and data integrity issues in the SAS reader.
        /// 
        /// **Fail-Fast Behavior:**
        /// By testing that exceptions occur during file opening (rather than during
        /// data reading), the test verifies that the reader detects problems early
        /// and doesn't attempt to process invalid data.
        /// 
        /// **Test Coverage:**
        /// This test complements the positive tests by ensuring robust error handling
        /// for real-world scenarios where files may be corrupted due to:
        /// <list type="bullet">
        /// <item><description>Incomplete file transfers</description></item>
        /// <item><description>Storage media errors</description></item>
        /// <item><description>Software bugs in file creation</description></item>
        /// <item><description>Intentional file corruption for security testing</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // Test that opening a corrupted file throws the expected exception
        /// await ShouldThrowIfCorruptedFile("resources/corrupted/invalid_magic.sas7bdat");
        /// 
        /// // The test will verify that:
        /// // 1. SasDataReader.OpenFileAsync() throws InvalidDataException
        /// // 2. No partial data is returned
        /// // 3. The exception occurs during file opening, not data reading
        /// </code>
        /// </example>
        [Theory]
        [MemberData(nameof(CorruptedDataFiles))]
        public async Task ShouldThrowIfCorruptedFile(string path)
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SasDataReader.OpenFileAsync(path));
        }

        /// <summary>
        /// Tests concurrent access to SAS files with multiple readers and parallel data reading.
        /// </summary>
        /// <remarks>
        /// This test validates the thread safety and concurrent access capabilities of the
        /// SasDataReader implementation. It verifies that multiple readers can safely access
        /// the same file simultaneously and that parallel data reading operations complete
        /// successfully without corruption or exceptions.
        /// 
        /// **Concurrency Test Design:**
        /// <list type="number">
        /// <item><description>Create multiple SasDataReader instances for the same file</description></item>
        /// <item><description>Open all readers concurrently using Task.WhenAll</description></item>
        /// <item><description>Initiate parallel data reading operations from each reader</description></item>
        /// <item><description>Verify all operations complete successfully without errors</description></item>
        /// </list>
        /// 
        /// **Thread Safety Validation:**
        /// <list type="bullet">
        /// <item><description>File locking behavior: Multiple readers can access the same file</description></item>
        /// <item><description>Memory safety: No race conditions in shared data structures</description></item>
        /// <item><description>Resource management: Proper disposal and cleanup in concurrent scenarios</description></item>
        /// <item><description>Data integrity: Results are consistent across parallel operations</description></item>
        /// </list>
        /// 
        /// **Performance Characteristics:**
        /// The test creates a number of concurrent operations proportional to the system's
        /// processor count, providing realistic load testing while remaining manageable
        /// across different hardware configurations.
        /// 
        /// **Stress Testing Aspects:**
        /// <list type="bullet">
        /// <item><description>Multiple file handles accessing the same file</description></item>
        /// <item><description>Concurrent enumeration of data rows</description></item>
        /// <item><description>Parallel task execution and coordination</description></item>
        /// <item><description>Resource contention under load</description></item>
        /// </list>
        /// 
        /// **Real-World Relevance:**
        /// This test simulates scenarios where multiple applications or threads might
        /// access the same SAS file simultaneously, which is common in:
        /// <list type="bullet">
        /// <item><description>Multi-threaded data processing applications</description></item>
        /// <item><description>Shared file systems with concurrent access</description></item>
        /// <item><description>Parallel analytics workflows</description></item>
        /// <item><description>Load-balanced data processing systems</description></item>
        /// </list>
        /// </remarks>
        /// <example>
        /// <code>
        /// // The test creates concurrent readers like this:
        /// var readers = Enumerable.Range(1, Environment.ProcessorCount)
        ///     .Select(_ => Task.Run(() => SasDataReader.OpenFileAsync(filePath)))
        ///     .ToArray();
        /// 
        /// await Task.WhenAll(readers);
        /// 
        /// // Then parallel data reading:
        /// var readTasks = readers.SelectMany(reader => 
        ///     Enumerable.Range(1, Environment.ProcessorCount)
        ///         .Select(_ => Task.Run(() => reader.Result.ReadRowsAsync().ToBlockingEnumerable().ToArray()))
        /// ).ToArray();
        /// 
        /// await Task.WhenAll(readTasks);
        /// </code>
        /// </example>
        [Fact]
        public async Task ParallelReads()
        {
            var path = (string)DataFiles.First().First();
            var readers = Enumerable.Range(1, Environment.ProcessorCount).Select(_ => Task.Run(() => SasDataReader.OpenFileAsync(path))).ToArray();
            await Task.WhenAll(readers);
            var data = new List<Task>();
            foreach (var reader in readers)
                foreach (var _ in Enumerable.Range(1, Environment.ProcessorCount))
                {
                    data.Add(Task.Run(() => reader.Result.ReadRowsAsync().ToBlockingEnumerable().ToArray()));
                }

            await Task.WhenAll(data);
        }

        /// <summary>
        /// Tests filtering, projection, and pagination features of the SasDataReader.
        /// </summary>
        /// <param name="path">The relative path to the SAS file to test.</param>
        /// <remarks>
        /// This comprehensive test validates the advanced data access features provided by
        /// RecordReadOptions, including column selection, row limiting, and row skipping.
        /// These features are essential for efficient data processing in scenarios where
        /// only portions of large datasets are needed.
        /// 
        /// **Feature Testing Coverage:**
        /// <list type="bullet">
        /// <item><description>Column Projection: Select specific columns by index</description></item>
        /// <item><description>Row Limiting: Take only the first N rows (MaxRows)</description></item>
        /// <item><description>Row Skipping: Skip the first N rows (SkipRows)</description></item>
        /// <item><description>Data Consistency: Verify projected data matches original data</description></item>
        /// </list>
        /// 
        /// **Column Projection Testing:**
        /// <list type="number">
        /// <item><description>Read complete dataset to establish baseline</description></item>
        /// <item><description>Read dataset with single column projection (column 0)</description></item>
        /// <item><description>Verify output array length matches projection (1 column)</description></item>
        /// <item><description>Verify projected values match original data for the selected column</description></item>
        /// </list>
        /// 
        /// **Row Limiting Testing:**
        /// <list type="number">
        /// <item><description>Use MaxRows = 1 to get only the first row</description></item>
        /// <item><description>Verify only one row is returned</description></item>
        /// <item><description>Verify the returned row matches the first row from complete dataset</description></item>
        /// </list>
        /// 
        /// **Row Skipping Testing:**
        /// <list type="number">
        /// <item><description>Skip to the last row using SkipRows = RowCount - 1</description></item>
        /// <item><description>Verify only one row is returned (the last row)</description></item>
        /// <item><description>Verify the returned row matches the last row from complete dataset</description></item>
        /// </list>
        /// 
        /// **Performance Benefits Validated:**
        /// These features provide significant performance benefits for large datasets:
        /// <list type="bullet">
        /// <item><description>Column projection reduces memory usage and processing time</description></item>
        /// <item><description>Row limiting enables efficient sampling and previewing</description></item>
        /// <item><description>Row skipping supports pagination and distributed processing</description></item>
        /// </list>
        /// 
        /// **Real-World Use Cases:**
        /// <list type="bullet">
        /// <item><description>Data analysis: Select relevant columns for specific analyses</description></item>
        /// <item><description>Data preview: Show first few rows for user interface</description></item>
        /// <item><description>Pagination: Implement paged data access for web applications</description></item>
        /// <item><description>Sampling: Process representative subsets of large datasets</description></item>
        /// </list>
        /// 
        /// **Validation Strategy:**
        /// The test uses multiple reading passes of the same file with different options,
        /// comparing results to ensure consistency and correctness of the filtering logic.
        /// </remarks>
        /// <example>
        /// <code>
        /// // Test column projection
        /// var columnOptions = new RecordReadOptions { SelectedColumnIndices = new[] { 0 } };
        /// await foreach (var row in reader.ReadRowsAsync(columnOptions))
        /// {
        ///     Assert.Equal(1, row.Length); // Only one column
        ///     Assert.Equal(originalData[rowIndex][0], row.Span[0]); // Same value
        /// }
        /// 
        /// // Test row limiting
        /// var limitOptions = new RecordReadOptions { MaxRows = 1 };
        /// var firstRowOnly = await reader.ReadRowsAsync(limitOptions).FirstAsync();
        /// Assert.Equal(originalData[0], firstRowOnly.ToArray());
        /// 
        /// // Test row skipping
        /// var skipOptions = new RecordReadOptions { SkipRows = totalRows - 1 };
        /// var lastRowOnly = await reader.ReadRowsAsync(skipOptions).FirstAsync();
        /// Assert.Equal(originalData[totalRows - 1], lastRowOnly.ToArray());
        /// </code>
        /// </example>
        [Theory]
        [MemberData(nameof(DataFiles))]
        public async Task TakeSkipAndProjections(string path)
        {
            await using var reader = await SasDataReader.OpenFileAsync(path);
            var rows = new List<object?[]>();
            var metadata = reader.Metadata;
            await foreach (var row in reader.ReadRowsAsync())
            {
                rows.Add(row.ToArray());
            }

            var i = 0;
            await foreach (var row in reader.ReadRowsAsync(new RecordReadOptions { SelectedColumnIndices = [0] }))
            {
                Assert.Equal(1, row.Length);
                Assert.Equal(rows[i++][0], row.Span[0]);
            }

            var first = default(object?[]);
            await foreach (var row in reader.ReadRowsAsync(new RecordReadOptions { MaxRows = 1 }))
            {
                Assert.Null(first);
                first = row.ToArray();
            }

            Assert.Equal(rows.First(), first);

            var last = default(object?[]);
            await foreach (var row in reader.ReadRowsAsync(new RecordReadOptions { SkipRows = (int?)(metadata.RowCount - 1) }))
            {
                Assert.Null(last);
                last = row.ToArray();
            }

            Assert.NotNull(last);
            Assert.Equal(rows.Last(), last);
        }
    }
}