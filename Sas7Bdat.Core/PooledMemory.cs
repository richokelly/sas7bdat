using System.Buffers;

namespace Sas7Bdat.Core;

/// <summary>
/// Provides a wrapper around pooled memory from the shared ArrayPool to enable efficient memory reuse.
/// </summary>
/// <typeparam name="T">The type of elements in the memory block.</typeparam>
/// <remarks>
/// This class manages a rented array from the shared ArrayPool and provides Memory and Span access
/// to a specified portion of that array. When disposed, the array is returned to the pool for reuse,
/// reducing garbage collection pressure and improving performance for frequently allocated buffers.
/// 
/// The actual rented array may be larger than the requested length, but the exposed Memory and Span
/// are limited to exactly the requested length for safety.
/// </remarks>
/// <example>
/// <code>
/// using var buffer = new PooledMemory&lt;byte&gt;(4096);
/// // Use buffer.Memory or buffer.Span for operations
/// buffer.Span.Fill(0);
/// // Array is automatically returned to pool when disposed
/// </code>
/// </example>
public sealed class PooledMemory<T> : IDisposable
{
    /// <summary>
    /// The array rented from the ArrayPool. May be larger than the requested length.
    /// </summary>
    private readonly T[] _array;

    /// <summary>
    /// Gets a Memory&lt;T&gt; view of the pooled array limited to the requested length.
    /// </summary>
    /// <value>
    /// A Memory&lt;T&gt; that provides access to exactly the number of elements requested
    /// when the PooledMemory was created.
    /// </value>
    /// <remarks>
    /// This Memory view is safe to use and will never exceed the bounds specified
    /// during construction, even though the underlying array may be larger.
    /// </remarks>
    public readonly Memory<T> Memory;

    /// <summary>
    /// Initializes a new instance of the PooledMemory class with the specified length.
    /// </summary>
    /// <param name="length">The number of elements needed in the memory block.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when length is negative.</exception>
    /// <remarks>
    /// This constructor rents an array from the shared ArrayPool. The actual array size
    /// may be larger than requested, but the exposed Memory will be limited to exactly
    /// the requested length.
    /// </remarks>
    public PooledMemory(int length)
    {
        _array = ArrayPool<T>.Shared.Rent(length);
        Memory = _array.AsMemory(0, length);
    }

    /// <summary>
    /// Gets a Span&lt;T&gt; view of the pooled memory.
    /// </summary>
    /// <value>
    /// A Span&lt;T&gt; that provides direct access to the memory elements
    /// limited to the requested length.
    /// </value>
    /// <remarks>
    /// The Span provides efficient, stack-allocated access to the memory contents.
    /// It's ideal for high-performance scenarios where you need direct memory access
    /// without heap allocations.
    /// </remarks>
    public Span<T> Span => Memory.Span;

    /// <summary>
    /// Returns the rented array to the ArrayPool and releases all resources.
    /// </summary>
    /// <remarks>
    /// After calling Dispose, the Memory and Span properties should not be used
    /// as they may reference memory that has been returned to the pool and could
    /// be reused by other code.
    /// 
    /// This method is idempotent - calling it multiple times is safe but has no effect
    /// after the first call.
    /// </remarks>
    public void Dispose()
    {
        ArrayPool<T>.Shared.Return(_array);
    }
}