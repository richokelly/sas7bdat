using System.Text.Json;
using Xunit.Abstractions;

namespace Sas7Bdat.Core.Tests
{
    public class SasDataReaderTests(ITestOutputHelper testOutputHelper)
    {
        public static IEnumerable<object[]> DataFiles => 
            Directory.EnumerateFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "supported"), "*.*", new EnumerationOptions { RecurseSubdirectories = true })
                .Where(path => !path.EndsWith(".json"))
                .Select(path => new[] {(object) path[AppDomain.CurrentDomain.BaseDirectory.Length..]})
            ;

        public static IEnumerable<object[]> CorruptedDataFiles =>
            Directory.EnumerateFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "corrupted"),
                    "*.*", new EnumerationOptions { RecurseSubdirectories = true })
                .Where(path => !path.EndsWith(".json"))
                .Select(path => new[] { (object)path[AppDomain.CurrentDomain.BaseDirectory.Length..] });

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

        [Theory]
        [MemberData(nameof(CorruptedDataFiles))]
        public async Task ShouldThrowIfCorruptedFile(string path)
        {
            await Assert.ThrowsAsync<InvalidDataException>(async () => await SasDataReader.OpenFileAsync(path));
        }

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