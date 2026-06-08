using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;

namespace ZeMail.UI.ViewModels;

public partial class EventEditorViewModel : ViewModelBase
{
    public Guid?  EventId   { get; init; }
    public Guid   AccountId { get; init; }
    public bool   IsEditMode => EventId.HasValue;

    [ObservableProperty] private string          _title          = string.Empty;
    [ObservableProperty] private string          _description    = string.Empty;
    [ObservableProperty] private string          _location       = string.Empty;
    [ObservableProperty] private DateTimeOffset? _startDate      = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan        _startTime      = TimeSpan.FromHours(9);
    [ObservableProperty] private DateTimeOffset? _endDate        = DateTimeOffset.Now;
    [ObservableProperty] private TimeSpan        _endTime        = TimeSpan.FromHours(10);
    [ObservableProperty] private bool            _isAllDay       = false;
    [ObservableProperty] private string          _statusMessage  = string.Empty;
    [ObservableProperty] private bool            _isSaving       = false;

    public string WindowTitle => IsEditMode ? "Termin bearbeiten" : "Neuer Termin";

    public event Action? OnSaved;
    public event Action? OnCancelled;
    public event Action? OnDeleted;

    [RelayCommand]
    private void Cancel() => OnCancelled?.Invoke();

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            StatusMessage = "Bitte Titel eingeben.";
            return;
        }

        IsSaving      = true;
        StatusMessage = "Speichern…";

        try
        {
            if (App.Services is null)
                throw new InvalidOperationException("Services nicht verfügbar.");

            using var scope = App.Services.CreateScope();
            var svc = scope.ServiceProvider
                           .GetRequiredService<ZeMail.Core.Interfaces.ICalendarService>();

            var startLocal = (StartDate?.Date ?? DateTime.Today).Add(StartTime);
            var endLocal   = (EndDate?.Date   ?? DateTime.Today).Add(EndTime);

            if (IsAllDay)
            {
                startLocal = StartDate?.Date ?? DateTime.Today;
                endLocal   = (EndDate?.Date ?? DateTime.Today).AddDays(1);
            }

            var startUtc = startLocal.ToUniversalTime();
            var endUtc   = endLocal.ToUniversalTime();

            if (endUtc <= startUtc)
            {
                StatusMessage = "Ende muss nach dem Start liegen.";
                IsSaving = false;
                return;
            }

            if (IsEditMode)
            {
                var existing = await svc.GetEventAsync(EventId!.Value);
                if (existing is null)
                {
                    StatusMessage = "Termin nicht gefunden.";
                    IsSaving = false;
                    return;
                }
                existing.Title       = Title;
                existing.Description = Description;
                existing.Location    = Location;
                existing.StartUtc    = startUtc;
                existing.EndUtc      = endUtc;
                existing.IsAllDay    = IsAllDay;
                await svc.UpdateEventAsync(existing);
            }
            else
            {
                await svc.CreateEventAsync(new CalendarEvent
                {
                    AccountId   = AccountId,
                    Title       = Title,
                    Description = Description,
                    Location    = Location,
                    StartUtc    = startUtc,
                    EndUtc      = endUtc,
                    IsAllDay    = IsAllDay,
                });
            }

            StatusMessage = "✓ Gespeichert";
            await Task.Delay(500);
            OnSaved?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Fehler: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (!IsEditMode || App.Services is null) return;

        IsSaving      = true;
        StatusMessage = "Löschen…";

        try
        {
            using var scope = App.Services.CreateScope();
            var svc = scope.ServiceProvider
                           .GetRequiredService<ZeMail.Core.Interfaces.ICalendarService>();
            await svc.DeleteEventAsync(EventId!.Value);
            OnDeleted?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"✗ Fehler: {ex.Message}";
        }
        finally
        {
            IsSaving = false;
        }
    }
}