using ZeMail.Core.Entities;
using ZeMail.Core.Enums;

namespace ZeMail.Core.Interfaces;

public interface ITaskService
{
    Task<List<TaskItem>> GetAllAsync(Guid accountId, bool includeCompleted = false, CancellationToken ct = default);
    Task<List<TaskItem>> GetDueTodayAsync(Guid accountId, CancellationToken ct = default);
    Task<TaskItem> CreateAsync(Guid accountId, string title, DateTime? dueUtc, TaskPriority priority, Guid? linkedMessageId = null, CancellationToken ct = default);
    Task UpdateAsync(TaskItem task, CancellationToken ct = default);
    Task CompleteAsync(Guid taskId, CancellationToken ct = default);
    Task DeleteAsync(Guid taskId, CancellationToken ct = default);
}