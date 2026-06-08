using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;

namespace ZeMail.UI.ViewModels;

public partial class ContactItemViewModel : ObservableObject
{
    public Guid    Id           { get; init; }
    public string  DisplayName  { get; init; } = string.Empty;
    public string  Organization { get; init; } = string.Empty;
    public string  PrimaryEmail { get; init; } = string.Empty;
    public string  PrimaryPhone { get; init; } = string.Empty;
    public string  Initials     => DisplayName.Length > 0
        ? string.Concat(DisplayName.Split(' ')
            .Where(w => w.Length > 0)
            .Take(2)
            .Select(w => w[0].ToString().ToUpper()))
        : "?";
}

public partial class ContactsViewModel : ViewModelBase
{
    // ── Listen ───────────────────────────────────────────────────────────────
    public ObservableCollection<ContactItemViewModel> AllContacts      { get; } = [];
    public ObservableCollection<ContactItemViewModel> FilteredContacts { get; } = [];

    // ── Selektion ────────────────────────────────────────────────────────────
    [ObservableProperty] private ContactItemViewModel? _selectedContact;

    // ── Edit-Felder ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _editDisplayName  = string.Empty;
    [ObservableProperty] private string _editOrganization = string.Empty;
    [ObservableProperty] private string _editEmail        = string.Empty;
    [ObservableProperty] private string _editPhone        = string.Empty;
    [ObservableProperty] private string _editNotes        = string.Empty;

    // ── Suche ────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _searchText = string.Empty;

    // ── Status ───────────────────────────────────────────────────────────────
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _hasSelection  = false;
    [ObservableProperty] private bool   _isNewContact  = false;

    public ContactsViewModel()
    {
        LoadContacts();
    }

    // ── Laden ────────────────────────────────────────────────────────────────
    public void LoadContacts()
    {
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        AllContacts.Clear();
        FilteredContacts.Clear();

        foreach (var c in db.Contacts.OrderBy(c => c.DisplayName).ToList())
        {
            var emails = TryParseJson(c.EmailsJson);
            var phones = TryParseJson(c.PhonesJson);

            var vm = new ContactItemViewModel
            {
                Id           = c.Id,
                DisplayName  = c.DisplayName,
                Organization = c.Organization ?? string.Empty,
                PrimaryEmail = emails.FirstOrDefault() ?? string.Empty,
                PrimaryPhone = phones.FirstOrDefault() ?? string.Empty,
            };
            AllContacts.Add(vm);
            FilteredContacts.Add(vm);
        }

        SelectedContact = FilteredContacts.FirstOrDefault();
    }

    partial void OnSearchTextChanged(string value)
    {
        FilteredContacts.Clear();
        var lower = value.ToLower();
        foreach (var c in AllContacts.Where(c =>
            string.IsNullOrEmpty(value) ||
            c.DisplayName.ToLower().Contains(lower) ||
            c.Organization.ToLower().Contains(lower) ||
            c.PrimaryEmail.ToLower().Contains(lower)))
        {
            FilteredContacts.Add(c);
        }
        SelectedContact = FilteredContacts.FirstOrDefault();
    }

    partial void OnSelectedContactChanged(ContactItemViewModel? value)
    {
        HasSelection  = value is not null;
        IsNewContact  = false;
        StatusMessage = string.Empty;

        if (value is null) return;

        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var c = db.Contacts.FirstOrDefault(x => x.Id == value.Id);
        if (c is null) return;

        EditDisplayName  = c.DisplayName;
        EditOrganization = c.Organization ?? string.Empty;
        EditEmail        = string.Join(", ", TryParseJson(c.EmailsJson));
        EditPhone        = string.Join(", ", TryParseJson(c.PhonesJson));
        EditNotes        = c.Notes ?? string.Empty;
    }

    // ── Neuer Kontakt ────────────────────────────────────────────────────────
    [RelayCommand]
    private void NewContact()
    {
        IsNewContact     = true;
        HasSelection     = true;
        SelectedContact  = null;
        EditDisplayName  = string.Empty;
        EditOrganization = string.Empty;
        EditEmail        = string.Empty;
        EditPhone        = string.Empty;
        EditNotes        = string.Empty;
        StatusMessage    = string.Empty;
    }

    // ── Speichern ────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(EditDisplayName))
        {
            StatusMessage = "Bitte Name eingeben.";
            return;
        }

        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var emails = EditEmail.Split(',', ';')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
        var phones = EditPhone.Split(',', ';')
            .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

        if (IsNewContact)
        {
            db.Add(new Contact
            {
                DisplayName  = EditDisplayName,
                Organization = string.IsNullOrEmpty(EditOrganization) ? null : EditOrganization,
                EmailsJson   = JsonSerializer.Serialize(emails),
                PhonesJson   = JsonSerializer.Serialize(phones),
                Notes        = string.IsNullOrEmpty(EditNotes) ? null : EditNotes,
            });
        }
        else if (SelectedContact is not null)
        {
            var c = db.Contacts.FirstOrDefault(x => x.Id == SelectedContact.Id);
            if (c is not null)
            {
                c.DisplayName  = EditDisplayName;
                c.Organization = string.IsNullOrEmpty(EditOrganization) ? null : EditOrganization;
                c.EmailsJson   = JsonSerializer.Serialize(emails);
                c.PhonesJson   = JsonSerializer.Serialize(phones);
                c.Notes        = string.IsNullOrEmpty(EditNotes) ? null : EditNotes;
                c.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync();
        StatusMessage = "✓ Gespeichert";
        LoadContacts();
    }

    // ── Löschen ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (SelectedContact is null || App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var c = db.Contacts.FirstOrDefault(x => x.Id == SelectedContact.Id);
        if (c is null) return;

        db.Remove(c);
        await db.SaveChangesAsync();
        StatusMessage = "Kontakt gelöscht.";
        LoadContacts();
    }

    // ── Hilfsmethoden ────────────────────────────────────────────────────────
    private static string[] TryParseJson(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}