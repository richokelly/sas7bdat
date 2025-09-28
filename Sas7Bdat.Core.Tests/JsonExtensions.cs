using System.Text;
using System.Text.Json;

namespace Sas7Bdat.Core.Tests;

/// <summary>
/// Provides extension methods for convenient JSON serialization in unit tests.
/// </summary>
/// <remarks>
/// This static class contains utility methods that simplify JSON serialization operations
/// commonly needed in unit testing scenarios. The methods provide a consistent and
/// convenient way to convert objects to JSON strings for comparison and validation purposes.
/// 
/// **Design Purpose:**
/// The extension methods are specifically designed for testing scenarios where:
/// <list type="bullet">
/// <item><description>Object comparison requires JSON string representation</description></item>
/// <item><description>Test output needs human-readable JSON formatting</description></item>
/// <item><description>Consistent serialization behavior is required across tests</description></item>
/// <item><description>Simple one-line conversion from objects to JSON strings is needed</description></item>
/// </list>
/// 
/// **Testing Integration:**
/// These extensions integrate seamlessly with testing frameworks by providing
/// standardized JSON representations that can be used for:
/// <list type="bullet">
/// <item><description>Assert.Equal() comparisons between expected and actual results</description></item>
/// <item><description>Test output logging for debugging and verification</description></item>
/// <item><description>Snapshot testing where JSON representations are stored and compared</description></item>
/// <item><description>Cross-platform test compatibility through consistent serialization</description></item>
/// </list>
/// 
/// **Performance Considerations:**
/// The methods use System.Text.Json for optimal performance and memory efficiency
/// compared to alternatives like Newtonsoft.Json, making them suitable for use
/// in performance-sensitive test scenarios or tests that process large datasets.
/// </remarks>
/// <example>
/// <code>
/// // Compare complex objects in unit tests
/// var expected = new SasFileMetadata { RowCount = 100, ColumnCount = 5 };
/// var actual = ParseSasFileMetadata(sasFile);
/// 
/// Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
/// 
/// // Log test data for debugging
/// testOutput.WriteLine($"Parsed metadata: {metadata.ToJsonString()}");
/// 
/// // Validate array contents
/// var expectedData = new object?[] { "John", 25, DateTime.Now };
/// var actualData = rowParser.Parse(rowBytes);
/// 
/// Assert.Equal(expectedData.ToJsonString(), actualData.ToJsonString());
/// </code>
/// </example>
public static class JsonExtensions
{
    /// <summary>
    /// Converts any object to its JSON string representation using System.Text.Json.
    /// </summary>
    /// <typeparam name="T">The type of object to serialize.</typeparam>
    /// <param name="obj">The object to convert to JSON string.</param>
    /// <returns>
    /// A JSON string representation of the object using UTF-8 encoding.
    /// </returns>
    /// <remarks>
    /// This extension method provides a convenient way to serialize any object to a JSON string
    /// using the default System.Text.Json serialization options. The method is particularly
    /// useful in unit testing scenarios where object comparison requires string representation.
    /// 
    /// **Serialization Behavior:**
    /// <list type="bullet">
    /// <item><description>Uses System.Text.Json default options for consistency</description></item>
    /// <item><description>Handles null values according to default JSON serialization rules</description></item>
    /// <item><description>Serializes public properties and fields based on .NET JSON conventions</description></item>
    /// <item><description>Converts to UTF-8 bytes first, then to string for optimal performance</description></item>
    /// </list>
    /// 
    /// **Type Support:**
    /// The method supports all types that System.Text.Json can serialize, including:
    /// <list type="bullet">
    /// <item><description>Primitive types (int, string, bool, etc.)</description></item>
    /// <item><description>DateTime and DateTimeOffset types</description></item>
    /// <item><description>Collections (arrays, lists, dictionaries)</description></item>
    /// <item><description>Custom objects with public properties</description></item>
    /// <item><description>Nullable types with proper null handling</description></item>
    /// </list>
    /// 
    /// **Error Handling:**
    /// If the object cannot be serialized (e.g., circular references, unsupported types),
    /// the method will throw a JsonException. In test scenarios, this typically indicates
    /// a test data setup issue rather than a serialization problem.
    /// 
    /// **Performance Characteristics:**
    /// <list type="bullet">
    /// <item><description>Uses efficient UTF-8 byte serialization internally</description></item>
    /// <item><description>Single string allocation for the final result</description></item>
    /// <item><description>Leverages System.Text.Json performance optimizations</description></item>
    /// <item><description>Suitable for high-frequency use in test loops</description></item>
    /// </list>
    /// 
    /// **Testing Usage Patterns:**
    /// <list type="bullet">
    /// <item><description>Object comparison: Compare serialized strings instead of object equality</description></item>
    /// <item><description>Test logging: Output readable representations of test data</description></item>
    /// <item><description>Snapshot testing: Generate consistent string representations</description></item>
    /// <item><description>Cross-platform testing: Ensure consistent serialization across environments</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="JsonException">
    /// Thrown when the object cannot be serialized to JSON, typically due to circular references,
    /// unsupported types, or serialization configuration issues.
    /// </exception>
    /// <exception cref="NotSupportedException">
    /// Thrown when attempting to serialize types that are not supported by System.Text.Json.
    /// </exception>
    /// <example>
    /// <code>
    /// // Simple object serialization
    /// var person = new { Name = "John", Age = 30 };
    /// var json = person.ToJsonString();
    /// // Result: {"Name":"John","Age":30}
    /// 
    /// // Array serialization
    /// var numbers = new[] { 1, 2, 3, 4, 5 };
    /// var arrayJson = numbers.ToJsonString();
    /// // Result: [1,2,3,4,5]
    /// 
    /// // Complex object with nested properties
    /// var metadata = new SasFileMetadata 
    /// { 
    ///     RowCount = 1000, 
    ///     DateCreated = DateTime.Parse("2023-01-01") 
    /// };
    /// var metadataJson = metadata.ToJsonString();
    /// 
    /// // Null handling
    /// string? nullString = null;
    /// var nullJson = nullString.ToJsonString();
    /// // Result: "null"
    /// 
    /// // Use in test assertions
    /// Assert.Equal(expected.ToJsonString(), actual.ToJsonString());
    /// 
    /// // Use in test output
    /// testOutput.WriteLine($"Test data: {testObject.ToJsonString()}");
    /// </code>
    /// </example>
    public static string ToJsonString<T>(this T obj)
    {
        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(obj));
    }
}