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

    // The list/page projection deliberately omits enc_thumbnail (the large BLOB): list rows show only the preview,
    // and thumbnails are fetched on demand via GetThumbnailAsync. This keeps a page's memory bounded and small.
    private const string ListColumns =
        "id, content_type, content_hash, enc_preview, enc_source, width, height, size_bytes, created_at, last_used_at, is_pinned";

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

        // No pooling: a single connection is held for the app lifetime, so Dispose closes it, checkpoints the WAL, and releases the file.
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
    public Task<HistoryPage> GetPageAsync(HistoryCursor? after, int limit, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            if (after is { } cursor)
            {
                // Keyset seek. The ORDER BY mixes directions (is_pinned DESC, last_used_at DESC, id ASC), so the
                // "strictly after the cursor" predicate must be the OR-expansion, not a single row-value tuple.
                cmd.CommandText =
                    $"""
                    SELECT {ListColumns} FROM entries
                    WHERE (is_pinned < $p)
                       OR (is_pinned = $p AND last_used_at < $t)
                       OR (is_pinned = $p AND last_used_at = $t AND id > $i)
                    ORDER BY is_pinned DESC, last_used_at DESC, id ASC
                    LIMIT $limit;
                    """;
                cmd.Parameters.AddWithValue("$p", cursor.IsPinned ? 1 : 0);
                cmd.Parameters.AddWithValue("$t", cursor.LastUsedAt.ToUnixTimeMilliseconds());

                // Bind the id as a BLOB so SQLite compares it byte-for-byte (memcmp), matching the index ordering.
                cmd.Parameters.AddWithValue("$i", cursor.Id.Value.ToByteArray());
            }
            else
            {
                cmd.CommandText =
                    $"""
                    SELECT {ListColumns} FROM entries
                    ORDER BY is_pinned DESC, last_used_at DESC, id ASC
                    LIMIT $limit;
                    """;
            }

            cmd.Parameters.AddWithValue("$limit", limit);

            var entries = new List<ClipboardEntry>();
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                entries.Add(MapListEntry(reader));
            }

            // A full page means there may be more; a short page means the source is exhausted.
            var next = entries.Count == limit ? HistoryCursor.After(entries[^1]) : (HistoryCursor?)null;
            return new HistoryPage(entries, next);
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task<byte[]?> GetThumbnailAsync(EntryId id, CancellationToken cancellationToken = default) =>
        WithConnectionAsync<byte[]?>(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT enc_thumbnail FROM entries WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id.Value.ToByteArray());

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
            {
                return null;
            }

            var encrypted = await reader.GetFieldValueAsync<byte[]>(0, cancellationToken);
            return _encryption.Decrypt(encrypted, Aad(id, "thumbnail"));
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
    public Task<int> DeleteExpiredAsync(DateTimeOffset capturedBefore, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM entries WHERE is_pinned = 0 AND created_at < $cutoff;";
            cmd.Parameters.AddWithValue("$cutoff", capturedBefore.ToUnixTimeMilliseconds());
            var removed = await cmd.ExecuteNonQueryAsync(cancellationToken);
            await ReclaimAsync(conn, removed, cancellationToken);
            return removed;
        },
            cancellationToken);

    /// <inheritdoc/>
    public Task<int> TrimAsync(int maxEntries, long maxTotalBytes, CancellationToken cancellationToken = default) =>
        WithConnectionAsync(
            async conn =>
        {
            // Rank unpinned entries most-recently-used first, with a running size sum, and delete those past the
            // count budget or past the byte budget (always keeping rank 1). One indexed pass, no row materialized.
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                """
                DELETE FROM entries WHERE id IN (
                  SELECT id FROM (
                    SELECT id,
                           ROW_NUMBER() OVER (ORDER BY last_used_at DESC, id ASC) AS rn,
                           SUM(size_bytes) OVER (ORDER BY last_used_at DESC, id ASC ROWS UNBOUNDED PRECEDING) AS running
                    FROM entries
                    WHERE is_pinned = 0)
                  WHERE rn > $maxEntries OR (rn > 1 AND running > $maxBytes));
                """;
            cmd.Parameters.AddWithValue("$maxEntries", maxEntries);
            cmd.Parameters.AddWithValue("$maxBytes", maxTotalBytes);
            var removed = await cmd.ExecuteNonQueryAsync(cancellationToken);
            await ReclaimAsync(conn, removed, cancellationToken);
            return removed;
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

            // Truncate the WAL and rebuild the file so freed (zeroed) pages are actually reclaimed, not just freelisted.
            await using var reclaim = conn.CreateCommand();
            reclaim.CommandText = "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;";
            await reclaim.ExecuteNonQueryAsync(cancellationToken);
            return 0;
        },
            cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_connection is not null)
        {
            // Checkpoint and truncate the WAL on clean exit so no -wal/-shm sidecar keeps recent ciphertext.
            try
            {
                using var checkpoint = _connection.CreateCommand();
                checkpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                checkpoint.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                // Best-effort checkpoint during shutdown; closing the connection still flushes the WAL.
            }

            _connection.Dispose();
        }

        _gate.Dispose();
    }

    // Returns freed pages to the OS after an eviction so the file follows the shrinking history. No-op when nothing
    // was removed. Relies on auto_vacuum=INCREMENTAL (set in EnsureConnectionAsync); harmless otherwise.
    private static async Task ReclaimAsync(SqliteConnection conn, int removed, CancellationToken ct)
    {
        if (removed <= 0)
        {
            return;
        }

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA incremental_vacuum;";
        await cmd.ExecuteNonQueryAsync(ct);
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

    // Migrates a legacy database (created before auto_vacuum was enabled) once: the connection pragma only takes
    // effect on a fresh file, so an existing one must be rewritten with VACUUM to switch to incremental mode.
    private static async Task MigrateToIncrementalAutoVacuumAsync(SqliteConnection conn, CancellationToken ct)
    {
        await using var autoVacuum = conn.CreateCommand();
        autoVacuum.CommandText = "PRAGMA auto_vacuum;";
        var mode = Convert.ToInt64(await autoVacuum.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
        if (mode == 2)
        {
            return;
        }

        await using var vacuum = conn.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        await vacuum.ExecuteNonQueryAsync(ct);
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
            // secure_delete zeroes freed content pages, so deleted/expired ciphertext does not linger in the freelist.
            // auto_vacuum=INCREMENTAL lets PRAGMA incremental_vacuum return freed pages to the OS after eviction, so
            // the file follows a shrinking history. (Set before any table exists; an existing file is migrated below.)
            pragma.CommandText =
                "PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL; PRAGMA secure_delete=ON; PRAGMA auto_vacuum=INCREMENTAL;";
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
                CREATE INDEX IF NOT EXISTS ix_entries_keyset ON entries(is_pinned DESC, last_used_at DESC, id ASC);
                CREATE INDEX IF NOT EXISTS ix_entries_pinned_created ON entries(is_pinned, created_at);
                DROP INDEX IF EXISTS ix_entries_pinned_used;
                """;
            await schema.ExecuteNonQueryAsync(ct);
        }

        await MigrateToIncrementalAutoVacuumAsync(conn, ct);

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

    // Maps a row from the ListColumns projection (no enc_thumbnail). Image rows carry their dimensions with an empty
    // thumbnail placeholder; the bytes are fetched on demand via GetThumbnailAsync when the row scrolls into view.
    private ClipboardEntry MapListEntry(DbDataReader r)
    {
        var id = new EntryId(new Guid(r.GetFieldValue<byte[]>(0)));
        var type = (ClipContentType)r.GetInt32(1);
        var hash = new ContentHash(r.GetString(2));
        var preview = Encoding.UTF8.GetString(_encryption.Decrypt(r.GetFieldValue<byte[]>(3), Aad(id, "preview")));

        ImagePreview? image = null;
        if (type == ClipContentType.Image)
        {
            var width = r.IsDBNull(5) ? 0 : r.GetInt32(5);
            var height = r.IsDBNull(6) ? 0 : r.GetInt32(6);
            image = new ImagePreview([], width, height);
        }

        var source = DeserializeSource(_encryption.Decrypt(r.GetFieldValue<byte[]>(4), Aad(id, "source")));
        var size = r.GetInt64(7);
        var created = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(8));
        var used = DateTimeOffset.FromUnixTimeMilliseconds(r.GetInt64(9));
        var pinned = r.GetInt32(10) != 0;

        return ClipboardEntry.Restore(id, type, hash, preview, image, size, source, created, used, pinned);
    }
}
