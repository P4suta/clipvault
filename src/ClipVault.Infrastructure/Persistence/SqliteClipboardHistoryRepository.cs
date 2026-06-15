using System.Data.Common;
using System.Text;
using ClipVault.Domain.Abstractions;
using ClipVault.Domain.Entities;
using ClipVault.Domain.ValueObjects;
using Microsoft.Data.Sqlite;

namespace ClipVault.Infrastructure.Persistence;

/// <summary>
/// History persistence using SQLite. The content, preview, thumbnail, and source are all encrypted with
/// <see cref="IEncryptionService"/> before being stored. Only the metadata needed for searching and sorting
/// (type, timestamps, pin state) is kept in plaintext. The full-size content is decrypted lazily via
/// <see cref="MaterializeAsync"/>. A single connection is serialized with an asynchronous semaphore (suited
/// to the low-concurrency access of a single user).
/// </summary>
public sealed class SqliteClipboardHistoryRepository : IClipboardHistoryRepository, IDisposable
{
    private const string SelectColumns =
        "id, content_type, content_hash, enc_preview, enc_thumbnail, enc_source, width, height, size_bytes, created_at, last_used_at, is_pinned";

    private readonly string _connectionString;
    private readonly IEncryptionService _encryption;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqliteClipboardHistoryRepository"/> class.
    /// </summary>
    /// <param name="options">The storage options that provide the database path.</param>
    /// <param name="encryption">The encryption service used to encrypt and decrypt stored content.</param>
    public SqliteClipboardHistoryRepository(ClipVaultStorageOptions options, IEncryptionService encryption)
    {
        _encryption = encryption;

        // Pooling is unnecessary because a single connection is held for the entire app lifetime.
        // Disabling it guarantees the connection closes on Dispose, checkpoints the WAL, and releases the file.
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.DatabasePath,
            Pooling = false,
        }.ToString();
    }

    /// <inheritdoc/>
    public Task<ClipboardEntry?> FindByHashAsync(ContentHash hash, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM entries WHERE content_hash = $hash;";
            cmd.Parameters.AddWithValue("$hash", hash.Value);

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken) ? MapEntry(reader) : null;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task AddAsync(ClipboardEntry entry, ClipContent content, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                INSERT INTO entries
                  (id, content_type, content_hash, enc_payload, enc_preview, enc_thumbnail, enc_source,
                   width, height, size_bytes, created_at, last_used_at, is_pinned)
                VALUES
                  ($id, $type, $hash, $payload, $preview, $thumbnail, $source,
                   $width, $height, $size, $created, $used, $pinned);
                """;
            cmd.Parameters.AddWithValue("$id", entry.Id.Value.ToByteArray());
            cmd.Parameters.AddWithValue("$type", (int)entry.ContentType);
            cmd.Parameters.AddWithValue("$hash", entry.Hash.Value);
            cmd.Parameters.AddWithValue("$payload", _encryption.Encrypt(content.Payload, Aad(entry.Id, "payload")));
            cmd.Parameters.AddWithValue("$preview", _encryption.Encrypt(Encoding.UTF8.GetBytes(entry.Preview), Aad(entry.Id, "preview")));
            cmd.Parameters.AddWithValue(
                "$thumbnail",
                entry.Image is { } img ? _encryption.Encrypt(img.Thumbnail, Aad(entry.Id, "thumbnail")) : DBNull.Value);
            cmd.Parameters.AddWithValue("$source", _encryption.Encrypt(SerializeSource(entry.Source), Aad(entry.Id, "source")));
            cmd.Parameters.AddWithValue("$width", (object?)entry.Image?.Width ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$height", (object?)entry.Image?.Height ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$size", entry.SizeInBytes);
            cmd.Parameters.AddWithValue("$created", entry.CapturedAt.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$used", entry.LastUsedAt.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$pinned", entry.IsPinned ? 1 : 0);

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task UpdateAsync(ClipboardEntry entry, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE entries SET last_used_at = $used, is_pinned = $pinned WHERE id = $id;";
            cmd.Parameters.AddWithValue("$used", entry.LastUsedAt.ToUnixTimeMilliseconds());
            cmd.Parameters.AddWithValue("$pinned", entry.IsPinned ? 1 : 0);
            cmd.Parameters.AddWithValue("$id", entry.Id.Value.ToByteArray());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task RemoveAsync(EntryId id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM entries WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value.ToByteArray());
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ClipboardEntry>> GetAllAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT {SelectColumns} FROM entries ORDER BY is_pinned DESC, last_used_at DESC;";

            var entries = new List<ClipboardEntry>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapEntry(reader));
            }

            return (IReadOnlyList<ClipboardEntry>)entries;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task<ClipContent?> MaterializeAsync(EntryId id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT content_type, enc_payload FROM entries WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value.ToByteArray());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var type = (ClipContentType)reader.GetInt32(0);
            var encrypted = await reader.GetFieldValueAsync<byte[]>(1, cancellationToken);
            var payload = _encryption.Decrypt(encrypted, Aad(id, "payload"));
            return new ClipContent(type, payload);
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task<int> CountAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM entries;";
            var result = await cmd.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM entries;";
            await cmd.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        },
            cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        _connection?.Dispose();
        _gate.Dispose();
    }

    // The AAD that binds each ciphertext to (entry id, field name). Prevents reuse of ciphertext across fields or rows.
    private static byte[] Aad(EntryId id, string field) => Encoding.UTF8.GetBytes($"{id.Value:N}:{field}");

    // Serialization of the source application (avoids System.Text.Json reflection to stay trim-safe).
    private static byte[] SerializeSource(SourceApplication source)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8);
        writer.Write(source.ProcessName);
        WriteNullable(writer, source.WindowTitle);
        WriteNullable(writer, source.ExecutablePath);
        writer.Flush();
        return stream.ToArray();

        static void WriteNullable(BinaryWriter w, string? value)
        {
            w.Write(value is not null);
            if (value is not null)
            {
                w.Write(value);
            }
        }
    }

    private static SourceApplication DeserializeSource(byte[] data)
    {
        using var stream = new MemoryStream(data);
        using var reader = new BinaryReader(stream, Encoding.UTF8);
        var process = reader.ReadString();
        var title = reader.ReadBoolean() ? reader.ReadString() : null;
        var path = reader.ReadBoolean() ? reader.ReadString() : null;
        return new SourceApplication(process, title, path);
    }

    private async Task<T> WithConnectionAsync<T>(Func<SqliteConnection, Task<T>> action, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var conn = await EnsureConnectionAsync(ct);
            return await action(conn);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<SqliteConnection> EnsureConnectionAsync(CancellationToken ct)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(ct);

        await using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
            await pragma.ExecuteNonQueryAsync(ct);
        }

        await using (var schema = conn.CreateCommand())
        {
            schema.CommandText =
                """
                CREATE TABLE IF NOT EXISTS entries(
                  id            BLOB PRIMARY KEY,
                  content_type  INTEGER NOT NULL,
                  content_hash  TEXT NOT NULL UNIQUE,
                  enc_payload   BLOB NOT NULL,
                  enc_preview   BLOB NOT NULL,
                  enc_thumbnail BLOB,
                  enc_source    BLOB NOT NULL,
                  width         INTEGER,
                  height        INTEGER,
                  size_bytes    INTEGER NOT NULL,
                  created_at    INTEGER NOT NULL,
                  last_used_at  INTEGER NOT NULL,
                  is_pinned     INTEGER NOT NULL DEFAULT 0
                );
                CREATE INDEX IF NOT EXISTS ix_entries_pinned_used ON entries(is_pinned DESC, last_used_at DESC);
                """;
            await schema.ExecuteNonQueryAsync(ct);
        }

        _connection = conn;
        return conn;
    }

    private ClipboardEntry MapEntry(DbDataReader r)
    {
        var id = new EntryId(new Guid(r.GetFieldValue<byte[]>(0)));
        var type = (ClipContentType)r.GetInt32(1);
        var hash = new ContentHash(r.GetString(2));
        var preview = Encoding.UTF8.GetString(_encryption.Decrypt(r.GetFieldValue<byte[]>(3), Aad(id, "preview")));

        ImagePreview? image = null;
        if (type == ClipContentType.Image && !r.IsDBNull(4))
        {
            var thumbnail = _encryption.Decrypt(r.GetFieldValue<byte[]>(4), Aad(id, "thumbnail"));
            var width = r.IsDBNull(6) ? 0 : r.GetInt32(6);
            var height = r.IsDBNull(7) ? 0 : r.GetInt32(7);
            image = new ImagePreview(thumbnail, width, height);
        }

        var source = DeserializeSource(_encryption.Decrypt(r.GetFieldValue<byte[]>(5), Aad(id, "source")));
        var size = r.GetInt64(8);
        var created = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(9));
        var used = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(10));
        var pinned = r.GetInt32(11) != 0;

        return ClipboardEntry.Restore(id, type, hash, preview, image, size, source, created, used, pinned);
    }
}
