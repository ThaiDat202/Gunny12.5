using System.Collections.Concurrent;
using System.Text.Json;
using GunnySuite.Core;

namespace GunnyPanel.Jobs;

public enum JobStatus { Pending, Running, Success, Failed, Cancelled }

public sealed class JobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Progress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Error { get; set; }
    public string? Result { get; set; }
    public List<string> Log { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool CancelRequested { get; set; }
}

/// <summary>Luu job trong memory + snapshot ra data/jobs.json.</summary>
public sealed class JobStore
{
    private readonly ConcurrentDictionary<Guid, JobRecord> _jobs = new();
    private readonly object _lock = new();
    private readonly string _file;

    public JobStore(IConfiguration cfg)
    {
        var dataDir = cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _file = Path.Combine(dataDir, "jobs.json");
        Load();
    }

    public IReadOnlyList<JobRecord> List(int max = 100)
    {
        lock (_lock)
            return _jobs.Values.OrderByDescending(j => j.CreatedAt).Take(max).ToList();
    }

    public JobRecord? Get(Guid id)
    {
        _jobs.TryGetValue(id, out var job);
        return job;
    }

    public void Add(JobRecord job)
    {
        _jobs[job.Id] = job;
        Save();
    }

    public void Save()
    {
        try
        {
            lock (_lock)
            {
                var list = _jobs.Values.OrderByDescending(j => j.CreatedAt).Take(300).ToList();
                File.WriteAllText(_file, JsonSerializer.Serialize(list));
            }
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var list = JsonSerializer.Deserialize<List<JobRecord>>(File.ReadAllText(_file)) ?? new();
            foreach (var job in list)
            {
                if (job.Status is JobStatus.Pending or JobStatus.Running)
                {
                    job.Status = JobStatus.Failed;
                    job.Error = "Bi ngat do panel restart";
                    job.FinishedAt = DateTime.Now;
                }
                _jobs[job.Id] = job;
            }
        }
        catch { }
    }
}

public sealed class JobExecutionContext
{
    public required JobRecord Job { get; init; }
    public required Dictionary<string, string> Parameters { get; init; }
    public required Action<int, string> Report { get; init; }
    public required AuditLog Audit { get; init; }
}

public interface IJobHandler
{
    string Type { get; }
    Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct);
}

public sealed class JobQueue
{
    private readonly BlockingCollection<Guid> _queue = new();
    private readonly JobStore _store;

    public JobQueue(JobStore store) => _store = store;

    public JobRecord Enqueue(string type, string title, Dictionary<string, string> parameters, string createdBy)
    {
        var job = new JobRecord { Type = type, Title = title, CreatedBy = createdBy, Parameters = parameters };
        _store.Add(job);
        _queue.Add(job.Id);
        return job;
    }

    public void RequestCancel(Guid id)
    {
        var job = _store.Get(id);
        if (job == null) return;
        job.CancelRequested = true;
        _store.Save();
    }

    public IEnumerable<Guid> Consume(CancellationToken ct) => _queue.GetConsumingEnumerable(ct);
}

/// <summary>Worker chay tuan tu cac job (1 job 1 luc).</summary>
public sealed class JobWorker : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly JobStore _store;
    private readonly IServiceScopeFactory _scopes;
    private readonly AuditLog _audit;

    public JobWorker(JobQueue queue, JobStore store, IServiceScopeFactory scopes, AuditLog audit)
    {
        _queue = queue;
        _store = store;
        _scopes = scopes;
        _audit = audit;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        foreach (var id in _queue.Consume(stoppingToken))
        {
            await RunOneAsync(id, stoppingToken);
        }
    }

    private async Task RunOneAsync(Guid id, CancellationToken stoppingToken)
    {
        var job = _store.Get(id);
        if (job == null) return;
        if (job.CancelRequested)
        {
            job.Status = JobStatus.Cancelled;
            job.FinishedAt = DateTime.Now;
            _store.Save();
            return;
        }

        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.Now;
        job.Log.Add($"[{DateTime.Now:HH:mm:ss}] bat dau");
        _store.Save();

        try
        {
            using var scope = _scopes.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IJobHandler>()
                .FirstOrDefault(h => h.Type == job.Type)
                ?? throw new InvalidOperationException($"Khong co handler cho job '{job.Type}'");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using var poll = new Timer(_ =>
            {
                if (!job.CancelRequested) return;
                try { cts.Cancel(); } catch { }
            }, null, 1000, 1000);

            var ctx = new JobExecutionContext
            {
                Job = job,
                Parameters = job.Parameters,
                Audit = _audit,
                Report = (percent, message) =>
                {
                    job.Progress = Math.Clamp(percent, 0, 100);
                    job.Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    if (job.Log.Count > 400) job.Log.RemoveRange(1, job.Log.Count - 400);
                    _store.Save();
                }
            };

            var result = await handler.RunAsync(ctx, cts.Token);
            job.Result = result;
            job.Progress = 100;
            job.Status = job.CancelRequested ? JobStatus.Cancelled : JobStatus.Success;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.Error = "Da huy";
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.Log.Add($"[ERR] {ex}");
        }
        finally
        {
            job.FinishedAt = DateTime.Now;
            job.Log.Add($"[{DateTime.Now:HH:mm:ss}] ket thuc: {job.Status}");
            _store.Save();
            _audit.Write(job.CreatedBy, "job." + job.Type, $"{job.Status} | {job.Title} | {job.Error ?? job.Result ?? ""}");
        }
    }
}
