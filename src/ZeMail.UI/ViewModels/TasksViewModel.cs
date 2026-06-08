using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;
using ZeMail.Core.Enums;
using ZeMail.Core.Interfaces;

namespace ZeMail.UI.ViewModels;

// ── Wrapper-VM für eine einzelne Aufgabe ────────────────────────────────────
public partial class TaskItemViewModel : ViewModelBase
{
    private readonly TaskItem _entity;
    public Guid Id          => _entity.Id;
    public Guid AccountId   => _entity.AccountId;
    public Guid? TaskListId => _entity.TaskListId;
    public int SortOrder    => _entity.SortOrder;

    [ObservableProperty] private string _title;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private DateTime? _dueDate;
    [ObservableProperty] private bool _isCompleted;
    [ObservableProperty] private bool _isImportant;
    [ObservableProperty] private bool _isMyDay;
    [ObservableProperty] private TaskPriority _priority;

    public string DueDateDisplay => DueDate.HasValue
        ? DueDate.Value.ToLocalTime().ToString("ddd, dd. MMM")
        : string.Empty;

    public bool HasDueDate => DueDate.HasValue;
    public bool HasNotes   => !string.IsNullOrWhiteSpace(Notes);

    public string PriorityIcon => Priority switch
    {
        TaskPriority.High => "⬆",
        TaskPriority.Low  => "⬇",
        _                 => ""
    };

    public TaskItemViewModel(TaskItem entity)
    {
        _entity      = entity;
        _title       = entity.Title;
        _notes       = entity.Notes;
        _dueDate     = entity.DueUtc;
        _isCompleted = entity.IsCompleted;
        _isImportant = entity.IsImportant;
        _isMyDay     = entity.IsMyDay;
        _priority    = entity.Priority;
    }

    public TaskItem ToEntity()
    {
        _entity.Title        = Title;
        _entity.Notes        = Notes;
        _entity.DueUtc       = DueDate?.ToUniversalTime();
        _entity.IsCompleted  = IsCompleted;
        _entity.IsImportant  = IsImportant;
        _entity.IsMyDay      = IsMyDay;
        _entity.Priority     = Priority;
        _entity.UpdatedAtUtc = DateTime.UtcNow;
        if (IsCompleted && _entity.CompletedAtUtc == null)
            _entity.CompletedAtUtc = DateTime.UtcNow;
        else if (!IsCompleted)
            _entity.CompletedAtUtc = null;
        return _entity;
    }

    public void SetSortOrder(int order) => _entity.SortOrder = order;

    partial void OnDueDateChanged(DateTime? value)
    {
        OnPropertyChanged(nameof(DueDateDisplay));
        OnPropertyChanged(nameof(HasDueDate));
    }

    partial void OnNotesChanged(string? value)
    {
        OnPropertyChanged(nameof(HasNotes));
    }
}

// ── Wrapper-VM für eine Task-Liste ──────────────────────────────────────────
public partial class TaskListViewModel : ViewModelBase
{
    private readonly TaskList? _entity;

    public Guid? Id          => _entity?.Id;
    public bool IsSystem     { get; }
    public string? SystemKey { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private string _color;
    [ObservableProperty] private int _taskCount;

    public bool CanDelete => !IsSystem;
    public bool CanRename => !IsSystem;

    public TaskListViewModel(TaskList entity)
    {
        _entity   = entity;
        _name     = entity.Name;
        _icon     = entity.Icon;
        _color    = entity.Color;
        IsSystem  = entity.IsSystem;
        SystemKey = entity.SystemKey;
    }

    public TaskListViewModel(string name, string icon, string color, string systemKey)
    {
        _entity   = null;
        _name     = name;
        _icon     = icon;
        _color    = color;
        IsSystem  = true;
        SystemKey = systemKey;
    }

    public TaskList? ToEntity() => _entity;
}

// ── Haupt-ViewModel ─────────────────────────────────────────────────────────
public partial class TasksViewModel : ViewModelBase, IDisposable
{
    private IServiceScope? _scope;
    private IZeMailDbContext? _db;

    // ── Prioritäts-Optionen ──────────────────────────────────────────────────
    public record PriorityOption(TaskPriority Value, string Label);

    public List<PriorityOption> PriorityOptions { get; } =
    [
        new(TaskPriority.Low,    "Niedrig"),
        new(TaskPriority.Normal, "Normal"),
        new(TaskPriority.High,   "Hoch"),
    ];

    public PriorityOption? SelectedPriority
    {
        get => PriorityOptions.FirstOrDefault(p => p.Value == SelectedTask?.Priority);
        set
        {
            if (SelectedTask is not null && value is not null)
                SelectedTask.Priority = value.Value;
            OnPropertyChanged();
        }
    }

    // ── Collections & State ─────────────────────────────────────────────────
    public ObservableCollection<TaskListViewModel> Lists { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentTasks))]
    private TaskListViewModel? _selectedList;

    private readonly List<TaskItemViewModel> _allTasks = [];
    public ObservableCollection<TaskItemViewModel> CurrentTasks { get; } = [];
    public ObservableCollection<TaskItemViewModel> CompletedTasks { get; } = [];

    [ObservableProperty] private bool _isCompletedView;
    [ObservableProperty] private TaskItemViewModel? _selectedTask;
    [ObservableProperty] private string _newTaskTitle = string.Empty;
    [ObservableProperty] private bool _isAddingTask;
    [ObservableProperty] private bool _isAddingList;
    [ObservableProperty] private string _newListName = string.Empty;
    [ObservableProperty] private bool _isRenamingList;
    [ObservableProperty] private string _renameListText = string.Empty;
    [ObservableProperty] private bool _isDetailOpen;
    [ObservableProperty] private TaskItemViewModel? _draggedTask;

    private List<Account> _accounts = [];
    private Guid _defaultAccountId;

    // ── Init ────────────────────────────────────────────────────────────────
    public TasksViewModel()
    {
        if (App.Services is not null)
        {
            _scope = App.Services.CreateScope();
            _db    = _scope.ServiceProvider.GetRequiredService<IZeMailDbContext>();
        }
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_db is null) return;
        _accounts         = await Task.Run(() => _db.Accounts.ToList());
        _defaultAccountId = _accounts.FirstOrDefault()?.Id ?? Guid.Empty;
        await BuildListsAsync();
        await LoadTasksAsync();
        SelectedList = Lists.FirstOrDefault();
    }

    private async Task BuildListsAsync()
    {
        Lists.Clear();
        Lists.Add(new TaskListViewModel("Mein Tag",      "☀",  "#ffaa00", "myday"));
        Lists.Add(new TaskListViewModel("Wichtig",       "⭐", "#ff6060", "important"));
        Lists.Add(new TaskListViewModel("Geplant",       "📅", "#60aaff", "planned"));
        Lists.Add(new TaskListViewModel("Alle Aufgaben", "☑",  "#7070ff", "all"));
        Lists.Add(new TaskListViewModel("Erledigt",      "✅", "#40a060", "completed"));

        if (_db is null) return;
        var dbLists = await Task.Run(() => _db.TaskLists
            .Where(l => !l.IsSystem)
            .OrderBy(l => l.SortOrder)
            .ThenBy(l => l.Name)
            .ToList());
        foreach (var l in dbLists)
            Lists.Add(new TaskListViewModel(l));
    }

    private async Task LoadTasksAsync()
    {
        if (_db is null) return;
        _allTasks.Clear();
        var entities = await Task.Run(() => _db.Tasks
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.CreatedAtUtc)
            .ToList());
        foreach (var e in entities)
            _allTasks.Add(new TaskItemViewModel(e));
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    private void RefreshCurrentTasks()
    {
        CurrentTasks.Clear();
        CompletedTasks.Clear();
        if (SelectedList is null) return;

        if (SelectedList.SystemKey == "completed")
        {
            IsCompletedView = true;
            foreach (var t in _allTasks.Where(t => t.IsCompleted).OrderByDescending(t => t.DueDate))
                CompletedTasks.Add(t);
            return;
        }

        IsCompletedView = false;
        IEnumerable<TaskItemViewModel> filtered = SelectedList.SystemKey switch
        {
            "myday"     => _allTasks.Where(t => t.IsMyDay     && !t.IsCompleted),
            "important" => _allTasks.Where(t => t.IsImportant && !t.IsCompleted),
            "planned"   => _allTasks.Where(t => t.HasDueDate  && !t.IsCompleted),
            "all"       => _allTasks.Where(t => !t.IsCompleted),
            _           => _allTasks.Where(t => t.TaskListId == SelectedList.Id && !t.IsCompleted)
        };
        foreach (var t in filtered.OrderBy(t => t.SortOrder).ThenByDescending(t => t.IsImportant).ThenBy(t => t.DueDate))
            CurrentTasks.Add(t);
    }

    private void UpdateTaskCounts()
    {
        foreach (var list in Lists)
        {
            list.TaskCount = list.SystemKey switch
            {
                "myday"     => _allTasks.Count(t => t.IsMyDay     && !t.IsCompleted),
                "important" => _allTasks.Count(t => t.IsImportant && !t.IsCompleted),
                "planned"   => _allTasks.Count(t => t.HasDueDate  && !t.IsCompleted),
                "all"       => _allTasks.Count(t => !t.IsCompleted),
                "completed" => _allTasks.Count(t => t.IsCompleted),
                _           => _allTasks.Count(t => t.TaskListId == list.Id && !t.IsCompleted)
            };
        }
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedPriority));
    }

    partial void OnSelectedListChanged(TaskListViewModel? value)
    {
        IsDetailOpen = false;
        SelectedTask = null;
        RefreshCurrentTasks();
    }

    // ── Drag&Drop ────────────────────────────────────────────────────────────
    [RelayCommand]
    private void MoveTask(TaskItemViewModel target)
    {
        if (DraggedTask is null || DraggedTask == target) return;
        var fromIndex = CurrentTasks.IndexOf(DraggedTask);
        var toIndex   = CurrentTasks.IndexOf(target);
        if (fromIndex < 0 || toIndex < 0) return;
        CurrentTasks.Move(fromIndex, toIndex);
        _ = PersistSortOrderAsync();
    }

    private async Task PersistSortOrderAsync()
    {
        if (_db is null) return;
        for (int i = 0; i < CurrentTasks.Count; i++)
        {
            CurrentTasks[i].SetSortOrder(i);
            CurrentTasks[i].ToEntity();
        }
        await _db.SaveChangesAsync();
    }

    // ── Erledigt-Aktionen ────────────────────────────────────────────────────
    [RelayCommand]
    private async Task RestoreAllCompleted()
    {
        if (_db is null) return;
        foreach (var t in _allTasks.Where(t => t.IsCompleted).ToList())
        { t.IsCompleted = false; t.ToEntity(); }
        await _db.SaveChangesAsync();
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private async Task DeleteAllCompleted()
    {
        if (_db is null) return;
        var completed = _allTasks.Where(t => t.IsCompleted).ToList();
        foreach (var t in completed)
        { _db.Remove(t.ToEntity()); _allTasks.Remove(t); }
        await _db.SaveChangesAsync();
        if (SelectedTask is not null && SelectedTask.IsCompleted)
        { SelectedTask = null; IsDetailOpen = false; }
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    // ── Task hinzufügen ─────────────────────────────────────────────────────
    [RelayCommand]
    private void BeginAddTask() { IsAddingTask = true; NewTaskTitle = string.Empty; }

    [RelayCommand]
    private async Task ConfirmAddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle) || _db is null)
        { IsAddingTask = false; return; }

        var entity = new TaskItem
        {
            AccountId    = _defaultAccountId,
            Title        = NewTaskTitle.Trim(),
            IsMyDay      = SelectedList?.SystemKey == "myday",
            IsImportant  = SelectedList?.SystemKey == "important",
            DueUtc       = SelectedList?.SystemKey == "planned" ? DateTime.UtcNow.Date : null,
            TaskListId   = SelectedList?.SystemKey is null ? SelectedList?.Id : null,
            SortOrder    = CurrentTasks.Count,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.Add(entity);
        await _db.SaveChangesAsync();

        var vm = new TaskItemViewModel(entity);
        _allTasks.Add(vm);
        RefreshCurrentTasks();
        UpdateTaskCounts();
        NewTaskTitle = string.Empty;
        IsAddingTask = false;
    }

    [RelayCommand]
    private void CancelAddTask() { IsAddingTask = false; NewTaskTitle = string.Empty; }

    // ── Task-Aktionen ───────────────────────────────────────────────────────
    [RelayCommand]
    private void SelectTask(TaskItemViewModel task) { SelectedTask = task; IsDetailOpen = true; }

    [RelayCommand]
    private async Task ToggleComplete(TaskItemViewModel task)
    {
        if (_db is null) return;
        task.IsCompleted = !task.IsCompleted;
        task.ToEntity();
        await _db.SaveChangesAsync();
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private async Task ToggleImportant(TaskItemViewModel task)
    {
        if (_db is null) return;
        task.IsImportant = !task.IsImportant;
        task.ToEntity();
        await _db.SaveChangesAsync();
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private async Task ToggleMyDay(TaskItemViewModel task)
    {
        if (_db is null) return;
        task.IsMyDay = !task.IsMyDay;
        task.ToEntity();
        await _db.SaveChangesAsync();
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private async Task SaveTaskDetail()
    {
        if (_db is null || SelectedTask is null) return;
        SelectedTask.ToEntity();
        await _db.SaveChangesAsync();
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private async Task DeleteTask(TaskItemViewModel task)
    {
        if (_db is null) return;
        _db.Remove(task.ToEntity());
        await _db.SaveChangesAsync();
        _allTasks.Remove(task);
        if (SelectedTask == task) { SelectedTask = null; IsDetailOpen = false; }
        RefreshCurrentTasks();
        UpdateTaskCounts();
    }

    [RelayCommand]
    private void CloseDetail() { IsDetailOpen = false; SelectedTask = null; }

    // ── Listen-Verwaltung ───────────────────────────────────────────────────
    [RelayCommand]
    private void BeginAddList() { IsAddingList = true; NewListName = string.Empty; }

    [RelayCommand]
    private async Task ConfirmAddList()
    {
        if (string.IsNullOrWhiteSpace(NewListName) || _db is null)
        { IsAddingList = false; return; }

        var entity = new TaskList
        {
            AccountId = _defaultAccountId,
            Name      = NewListName.Trim(),
            Color     = "#7070ff",
            Icon      = "📋",
            IsSystem  = false,
            SortOrder = Lists.Count(l => !l.IsSystem)
        };

        _db.Add(entity);
        await _db.SaveChangesAsync();

        var vm = new TaskListViewModel(entity);
        Lists.Add(vm);
        SelectedList = vm;
        NewListName  = string.Empty;
        IsAddingList = false;
    }

    [RelayCommand]
    private void CancelAddList() { IsAddingList = false; NewListName = string.Empty; }

    [RelayCommand]
    private void NavigateList(TaskListViewModel list) => SelectedList = list;

    [RelayCommand]
    private void BeginRenameList(TaskListViewModel list)
    {
        if (!list.CanRename) return;
        SelectedList   = list;
        RenameListText = list.Name;
        IsRenamingList = true;
    }

    [RelayCommand]
    private async Task ConfirmRenameList()
    {
        if (SelectedList is null || string.IsNullOrWhiteSpace(RenameListText) || _db is null)
        { IsRenamingList = false; return; }

        SelectedList.Name = RenameListText.Trim();
        var entity = SelectedList.ToEntity();
        if (entity is not null) { entity.Name = SelectedList.Name; await _db.SaveChangesAsync(); }
        IsRenamingList = false;
    }

    [RelayCommand]
    private void CancelRenameList() => IsRenamingList = false;

    [RelayCommand]
    private async Task DeleteList(TaskListViewModel list)
    {
        if (!list.CanDelete || _db is null) return;
        var entity = list.ToEntity();
        if (entity is not null)
        {
            var tasks = await Task.Run(() => _db.Tasks.Where(t => t.TaskListId == list.Id).ToList());
            foreach (var t in tasks) t.TaskListId = null;
            _db.Remove(entity);
            await _db.SaveChangesAsync();
        }
        Lists.Remove(list);
        if (SelectedList == list) SelectedList = Lists.FirstOrDefault();
        await LoadTasksAsync();
    }

    public void Dispose() => _scope?.Dispose();
}