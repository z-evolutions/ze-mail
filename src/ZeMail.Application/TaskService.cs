using Microsoft.Extensions.Logging;
using ZeMail.Core.Entities;
using ZeMail.Core.Enums;
using ZeMail.Core.Interfaces;

namespace ZeMail.Application;

public class TaskService : ITaskService
{
    private readonly IZeMailDbContext _db;
    private readonly ILogger<TaskService> _logger;

    public TaskService(IZeMailDbContext db, ILogger<TaskService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<List<TaskItem>> GetAllAsync(Guid accountId, bool includeCompleted = false, CancellationToken ct = default)
    {
        var query = _db.Tasks.Where(t => t.AccountId == accountId);

        if (!includeCompleted)
            query = query.Where(t => !t.IsCompleted);

        return Task.FromResult(query
            .OrderBy(t => t.DueUtc)
            .ThenByDescending(t => t.Priority)
            .ToList());
    }

    public Task<List<TaskItem>> GetDueTodayAsync(Guid accountId, CancellationToken ct = default)
    {
        var todayEnd = DateTime.UtcNow.Date.AddDays(1);
        return Task.FromResult(_db.Tasks
            .Where(t => t.AccountId == accountId &&
                        !t.IsCompleted &&
                        t.DueUtc.HasValue &&
                        t.DueUtc < todayEnd)
            .OrderBy(t => t.DueUtc)
            .ToList());
    }

    public async Task<TaskItem> CreateAsync(Guid accountId, string title, DateTime? dueUtc, TaskPriority priority, Guid? linkedMessageId = null, CancellationToken ct = default)
    {
        var task = new TaskItem
        {
            AccountId       = accountId,
            Title           = title,
            DueUtc          = dueUtc,
            Priority        = priority,
            LinkedMessageId = linkedMessageId,
            CreatedAtUtc    = DateTime.UtcNow,
            UpdatedAtUtc    = DateTime.UtcNow
        };

        _db.Add(task);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Task '{Title}' created for account {AccountId}", title, accountId);
        return task;
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken ct = default)
    {
        task.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task CompleteAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _db.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;

        task.IsCompleted    = true;
        task.CompletedAtUtc = DateTime.UtcNow;
        task.UpdatedAtUtc   = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Task {TaskId} completed", taskId);
    }

    public async Task DeleteAsync(Guid taskId, CancellationToken ct = default)
    {
        var task = _db.Tasks.FirstOrDefault(t => t.Id == taskId);
        if (task is null) return;
        _db.Remove(task);
        await _db.SaveChangesAsync(ct);
    }
}