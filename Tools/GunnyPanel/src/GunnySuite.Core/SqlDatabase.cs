using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace GunnySuite.Core;

public sealed class SqlResult
{
    public List<string> Columns { get; } = new();
    public List<string?[]> Rows { get; } = new();
    public int TotalRead { get; set; }
    public bool Truncated { get; set; }
    public string? Error { get; set; }
    public bool Ok => Error == null;

    public string? FirstCell() => Rows.Count > 0 && Rows[0].Length > 0 ? Rows[0][0] : null;
}

public sealed class SqlDatabase
{
    private readonly Dictionary<string, string> _connections;

    public SqlDatabase(IReadOnlyDictionary<string, string> connections)
    {
        _connections = new Dictionary<string, string>(connections, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names => _connections.Keys;
    public bool Has(string name) => _connections.ContainsKey(name);
    public string? GetConnectionString(string name) => _connections.TryGetValue(name, out var cs) ? cs : null;

    public Task<SqlResult> QueryAsync(string connectionName, string sql, int maxRows = 500, int timeoutSeconds = 30, CancellationToken ct = default)
        => QueryAsync(connectionName, sql, null, maxRows, timeoutSeconds, ct);

    public async Task<SqlResult> QueryAsync(string connectionName, string sql, IDictionary<string, object?>? parameters, int maxRows = 500, int timeoutSeconds = 30, CancellationToken ct = default)
    {
        var result = new SqlResult();
        if (!_connections.TryGetValue(connectionName, out var cs))
        {
            result.Error = $"Khong tim thay connection '{connectionName}'";
            return result;
        }

        try
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
            AddParameters(cmd, parameters);
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            for (var i = 0; i < reader.FieldCount; i++) result.Columns.Add(reader.GetName(i));
            while (await reader.ReadAsync(ct))
            {
                result.TotalRead++;
                if (result.Rows.Count >= maxRows)
                {
                    result.Truncated = true;
                    continue;
                }
                var row = new string?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i)
                        ? null
                        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                }
                result.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public async Task<(int rows, string? error)> ExecuteAsync(string connectionName, string sql, IDictionary<string, object?>? parameters = null, int timeoutSeconds = 60, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(connectionName, out var cs))
            return (0, $"Khong tim thay connection '{connectionName}'");
        try
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
            AddParameters(cmd, parameters);
            return (await cmd.ExecuteNonQueryAsync(ct), null);
        }
        catch (Exception ex)
        {
            return (0, ex.Message);
        }
    }

    /// <summary>
    /// Chay nhieu cau lenh trong 1 transaction (dung cho import XML -> SQL).
    /// Progress: (da xong, tong so).
    /// </summary>
    public async Task<(int rows, string? error)> ExecuteBatchAsync(
        string connectionName,
        IReadOnlyList<string> statements,
        int timeoutSeconds = 180,
        Action<int, int>? progress = null,
        CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(connectionName, out var cs))
            return (0, $"Khong tim thay connection '{connectionName}'");

        var totalRows = 0;
        await using var conn = new SqlConnection(cs);
        try
        {
            await conn.OpenAsync(ct);
            await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);
            try
            {
                for (var i = 0; i < statements.Count; i++)
                {
                    ct.ThrowIfCancellationRequested();
                    await using var cmd = new SqlCommand(statements[i], conn, tx) { CommandTimeout = timeoutSeconds };
                    totalRows += await cmd.ExecuteNonQueryAsync(ct);
                    progress?.Invoke(i + 1, statements.Count);
                }
                await tx.CommitAsync(ct);
                return (totalRows, null);
            }
            catch (Exception ex)
            {
                try { await tx.RollbackAsync(CancellationToken.None); } catch { }
                return (totalRows, ex.Message);
            }
        }
        catch (Exception ex)
        {
            return (totalRows, ex.Message);
        }
    }

    public async Task<(bool ok, string text)> TestAsync(string connectionName, CancellationToken ct = default)
    {
        var r = await QueryAsync(connectionName, "SELECT @@VERSION AS V", 1, 8, ct);
        if (!r.Ok) return (false, r.Error ?? "unknown error");
        var text = r.FirstCell() ?? "";
        return (true, text.Split('\n')[0].Trim());
    }

    private static void AddParameters(SqlCommand cmd, IDictionary<string, object?>? parameters)
    {
        if (parameters == null) return;
        foreach (var kv in parameters)
            cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);
    }
}
