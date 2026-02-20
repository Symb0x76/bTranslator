using bTranslator.Application.Abstractions;
using bTranslator.Infrastructure.Persistence.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace bTranslator.Infrastructure.Persistence.Services;

public sealed class SqliteSettingsStore : ISettingsStore
{
    private readonly string _connectionString;

    public SqliteSettingsStore(IOptions<PersistenceOptions> options)
    {
        Directory.CreateDirectory(options.Value.RootDirectory);
        var dbPath = Path.Combine(options.Value.RootDirectory, options.Value.DatabaseName);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();

        Initialize();
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "INSERT INTO settings(key, value) VALUES ($key, $value) ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        cmd.Parameters.AddWithValue("$key", key);
        cmd.Parameters.AddWithValue("$value", value);
        await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT value FROM settings WHERE key = $key LIMIT 1;";
        cmd.Parameters.AddWithValue("$key", key);
        var value = await cmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value as string;
    }

    private void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var wal = connection.CreateCommand();
        wal.CommandText = "PRAGMA journal_mode=WAL;";
        wal.ExecuteNonQuery();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "CREATE TABLE IF NOT EXISTS settings(key TEXT PRIMARY KEY, value TEXT NOT NULL);";
        cmd.ExecuteNonQuery();
    }
}

