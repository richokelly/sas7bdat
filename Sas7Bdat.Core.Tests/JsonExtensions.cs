using System.Text;
using System.Text.Json;

namespace Sas7Bdat.Core.Tests;

public static class JsonExtensions
{
    public static string ToJsonString<T>(this T obj)
    {
        return Encoding.UTF8.GetString(JsonSerializer.SerializeToUtf8Bytes(obj));
    }
}