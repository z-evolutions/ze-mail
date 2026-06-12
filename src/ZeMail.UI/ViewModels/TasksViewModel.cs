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

    // PathData für System-Listen (PathIcon in Sidebar), null für custom Listen (Emoji)
    public string? PathData  { get; }
    public string PathColor  { get; }
    public bool HasPathIcon  => PathData is not null;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _icon;
    [ObservableProperty] private string _color;
    [ObservableProperty] private int _taskCount;

    public bool CanDelete => !IsSystem;
    public bool CanRename => !IsSystem;

    // System-Listen mit PathIcon
    public TaskListViewModel(string name, string pathData, string pathColor, string color, string systemKey)
    {
        _entity   = null;
        _name     = name;
        _icon     = string.Empty;
        _color    = color;
        IsSystem  = true;
        SystemKey = systemKey;
        PathData  = pathData;
        PathColor = pathColor;
    }

    // Custom Listen mit Emoji-Icon
    public TaskListViewModel(TaskList entity)
    {
        _entity   = entity;
        _name     = entity.Name;
        _icon     = entity.Icon;
        _color    = entity.Color;
        IsSystem  = entity.IsSystem;
        SystemKey = entity.SystemKey;
        PathData  = null;
        PathColor = entity.Color;
    }

    public TaskList? ToEntity() => _entity;
}

// ── Haupt-ViewModel ─────────────────────────────────────────────────────────
public partial class TasksViewModel : ViewModelBase, IDisposable
{
    private IServiceScope? _scope;
    private IZeMailDbContext? _db;

    // Pfade für System-Listen
    private const string PathSun      = "M12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9M12,2L14.39,5.42C13.65,5.15 12.84,5 12,5C11.16,5 10.35,5.15 9.61,5.42L12,2M3.34,7L7.5,6.65C6.9,7.16 6.36,7.78 5.94,8.5C5.5,9.24 5.25,10 5.11,10.79L3.34,7M3.36,17L5.12,13.23C5.26,14 5.53,14.78 5.95,15.5C6.37,16.24 6.91,16.86 7.5,17.37L3.36,17M20.65,7L18.88,10.79C18.74,10 18.47,9.23 18.05,8.5C17.63,7.78 17.1,7.15 16.5,6.64L20.65,7M20.64,17L16.5,17.36C17.09,16.85 17.62,16.22 18.04,15.5C18.46,14.77 18.73,14 18.87,13.21L20.64,17M12,22L9.59,18.56C10.33,18.83 11.14,19 12,19C12.82,19 13.63,18.83 14.37,18.56L12,22Z";
    private const string PathAlert    = "M13,13H11V7H13M13,17H11V15H13M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2Z";
    private const string PathCalendar = "M19,19H5V8H19M16,1V3H8V1H6V3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3H18V1M17,13H12V18H17V13Z";
    private const string PathAll      = "M20,11H23V13H20V11M1,11H4V13H1V11M13,1V4H11V1H13M4.92,3.5L7.05,5.64L5.63,7.05L3.5,4.93L4.92,3.5M16.95,5.63L19.07,3.5L20.5,4.93L18.36,7.05L16.95,5.63M12,6A6,6 0 0,1 18,12C18,14.22 16.79,16.16 15,17.2V19A1,1 0 0,1 14,20H10A1,1 0 0,1 9,19V17.2C7.21,16.16 6,14.22 6,12A6,6 0 0,1 12,6M14,21V22A1,1 0 0,1 13,23H11A1,1 0 0,1 10,22V21H14Z";
    private const string PathCheck    = "M21,7L9,19L3.5,13.5L4.91,12.09L9,16.17L19.59,5.59L21,7Z";

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
        Lists.Add(new TaskListViewModel("Mein Tag",      PathSun,      "#ffaa00", "#ffaa00", "myday"));
        Lists.Add(new TaskListViewModel("Wichtig",       PathAlert,    "#ff6060", "#ff6060", "important"));
        Lists.Add(new TaskListViewModel("Geplant",       PathCalendar, "#60aaff", "#60aaff", "planned"));
        Lists.Add(new TaskListViewModel("Alle Aufgaben", PathAll,      "#7070ff", "#7070ff", "all"));
        Lists.Add(new TaskListViewModel("Erledigt",      PathCheck,    "#40a060", "#40a060", "completed"));

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