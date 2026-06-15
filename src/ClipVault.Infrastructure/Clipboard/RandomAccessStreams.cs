using Windows.Storage.Streams;

namespace ClipVault.Infrastructure.Clipboard;

/// <summary>
/// Helpers for converting between <see cref="byte"/> arrays and the WinRT <see cref="IRandomAccessStream"/>.
/// </summary>
internal static class RandomAccessStreams
{
    /// <summary>
    /// Creates an in-memory random-access stream populated with the given bytes.
    /// </summary>
    /// <param name="bytes">The bytes to write into the stream.</param>
    /// <returns>A task whose result is the populated stream, positioned at the start.</returns>
    public static async Task<InMemoryRandomAccessStream> FromBytesAsync(byte[] bytes)
    {
        var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            await writer.StoreAsync();
            await writer.FlushAsync();
            writer.DetachStream();
        }

        stream.Seek(0);
        return stream;
    }

    /// <summary>
    /// Reads the entire contents of the given random-access stream into a byte array.
    /// </summary>
    /// <param name="stream">The stream to read from.</param>
    /// <returns>A task whose result is the bytes read from the stream.</returns>
    public static async Task<byte[]> ToBytesAsync(IRandomAccessStream stream)
    {
        var bytes = new byte[stream.Size];
        using var reader = new DataReader(stream.GetInputStreamAt(0));
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        return bytes;
    }
}
