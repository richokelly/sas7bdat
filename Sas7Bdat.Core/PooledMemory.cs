using System.Buffers;

namespace Sas7Bdat.Core;

public sealed class PooledMemory<T> : IDisposable
{
    private readonly T[] _array;

    public readonly Memory<T> Memory;

    public PooledMemory(int length)
    {
        _array = ArrayPool<T>.Shared.Rent(length);
        Memory = _array.AsMemory(0, length);
    }

    public Span<T> Span => Memory.Span;

    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array);
    }
}