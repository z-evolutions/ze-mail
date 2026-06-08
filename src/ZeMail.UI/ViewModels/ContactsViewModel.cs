using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using ZeMail.Core.Entities;
using ZeMail.UI.Views;

namespace ZeMail.UI.ViewModels;

public partial class ContactItemViewModel : ObservableObject
{
    public Guid   Id           { get; init; }
    public string DisplayName  { get; init; } = string.Empty;
    public string Organization { get; init; } = string.Empty;
    public string Department   { get; init; } = string.Empty;
    public string PrimaryEmail { get; init; } = string.Empty;
    public string PrimaryPhone { get; init; } = string.Empty;
    public string Website      { get; init; } = string.Empty;
    public string Street       { get; init; } = string.Empty;
    public string City         { get; init; } = string.Empty;
    public string PostalCode   { get; init; } = string.Empty;
    public string Country      { get; init; } = string.Empty;
    public string Notes        { get; init; } = string.Empty;
    public string[] AllEmails  { get; init; } = [];
    public string[] AllPhones  { get; init; } = [];

    public string Initials => DisplayName.Length > 0
        ? string.Concat(DisplayName.Split(' ')
            .Where(w => w.Length > 0).Take(2)
            .Select(w => w[0].ToString().ToUpper()))
        : "?";

    [ObservableProperty] private Bitmap? _avatarBitmap;
    public bool HasAvatar => AvatarBitmap is not null;

    public async Task LoadAvatarAsync()
    {
        if (string.IsNullOrEmpty(PrimaryEmail)) return;
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(PrimaryEmail.Trim().ToLower()));
        var hex  = Convert.ToHexString(hash).ToLower();
        var url  = $"https://www.gravatar.com/avatar/{hex}?d=404&s=80";
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await http.GetAsync(url);
            if (resp.IsSuccessStatusCode)
            {
                var bytes = await resp.Content.ReadAsByteArrayAsync();
                using var ms = new MemoryStream(bytes);
                AvatarBitmap = new Bitmap(ms);
                OnPropertyChanged(nameof(HasAvatar));
            }
        }
        catch { }
    }
}

public partial class ContactGroupViewModel : ObservableObject
{
    public Guid   Id    { get; init; }
    public string Name  { get; init; } = string.Empty;
    public string Icon  { get; init; } = "👥";
    public string Color { get; init; } = "#3a3aff";
}

public partial class ContactsViewModel : ViewModelBase
{
    public ObservableCollection<ContactItemViewModel>  AllContacts      { get; } = [];
    public ObservableCollection<ContactItemViewModel>  FilteredContacts { get; } = [];
    public ObservableCollection<ContactGroupViewModel> Groups           { get; } = [];

    [ObservableProperty] private ContactItemViewModel?  _selectedContact;
    [ObservableProperty] private ContactGroupViewModel? _selectedGroup;

    [ObservableProperty] private string _editDisplayName  = string.Empty;
    [ObservableProperty] private string _editOrganization = string.Empty;
    [ObservableProperty] private string _editDepartment   = string.Empty;
    [ObservableProperty] private string _editEmail        = string.Empty;
    [ObservableProperty] private string _editPhone        = string.Empty;
    [ObservableProperty] private string _editWebsite      = string.Empty;
    [ObservableProperty] private string _editStreet       = string.Empty;
    [ObservableProperty] private string _editCity         = string.Empty;
    [ObservableProperty] private string _editPostalCode   = string.Empty;
    [ObservableProperty] private string _editCountry      = string.Empty;
    [ObservableProperty] private string _editNotes        = string.Empty;

    [ObservableProperty] private string _editGroupName   = string.Empty;
    [ObservableProperty] private string _editGroupIcon   = "👥";
    [ObservableProperty] private bool   _isGroupEditMode = false;
    [ObservableProperty] private bool   _isNewGroup      = false;

    [ObservableProperty] private string _searchText    = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool   _hasSelection  = false;
    [ObservableProperty] private bool   _isNewContact  = false;
    [ObservableProperty] private bool   _isEditMode    = false;

    public ContactsViewModel()
    {
        LoadGroups();
        LoadContacts();
    }

    public void LoadGroups()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        Groups.Clear();
        Groups.Add(new ContactGroupViewModel { Id = Guid.Empty, Name = "Alle Kontakte", Icon = "👤" });
        foreach (var g in db.ContactGroups.OrderBy(g => g.Name).ToList())
        {
            Groups.Add(new ContactGroupViewModel
            {
                Id    = g.Id,
                Name  = g.Name,
                Icon  = g.Icon  ?? "👥",
                Color = g.Color ?? "#3a3aff",
            });
        }
        SelectedGroup = Groups.FirstOrDefault();
    }

    public void LoadContacts()
    {
        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var groupId  = SelectedGroup?.Id ?? Guid.Empty;
        var contacts = groupId == Guid.Empty
            ? db.Contacts.OrderBy(c => c.DisplayName).ToList()
            : db.ContactGroupMembers
                .Where(m => m.GroupId == groupId)
                .Select(m => m.Contact)
                .OrderBy(c => c.DisplayName)
                .ToList();

        AllContacts.Clear();
        FilteredContacts.Clear();

        foreach (var c in contacts)
        {
            var emails = TryParseJson(c.EmailsJson);
            var phones = TryParseJson(c.PhonesJson);
            var vm = new ContactItemViewModel
            {
                Id           = c.Id,
                DisplayName  = c.DisplayName,
                Organization = c.Organization ?? string.Empty,
                Department   = c.Department   ?? string.Empty,
                PrimaryEmail = emails.FirstOrDefault() ?? string.Empty,
                PrimaryPhone = phones.FirstOrDefault() ?? string.Empty,
                Website      = c.Website    ?? string.Empty,
                Street       = c.Street     ?? string.Empty,
                City         = c.City       ?? string.Empty,
                PostalCode   = c.PostalCode ?? string.Empty,
                Country      = c.Country    ?? string.Empty,
                Notes        = c.Notes      ?? string.Empty,
                AllEmails    = emails,
                AllPhones    = phones,
            };
            AllContacts.Add(vm);
            FilteredContacts.Add(vm);
            _ = vm.LoadAvatarAsync();
        }

        SelectedContact = FilteredContacts.FirstOrDefault();
    }

    partial void OnSelectedGroupChanged(ContactGroupViewModel? value)
    {
        SearchText = string.Empty;
        LoadContacts();
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
        HasSelection  = value is not null || IsNewContact;
        IsEditMode    = false;
        IsNewContact  = false;
        StatusMessage = string.Empty;
        if (value is null) return;

        EditDisplayName  = value.DisplayName;
        EditOrganization = value.Organization;
        EditDepartment   = value.Department;
        EditEmail        = string.Join(", ", value.AllEmails);
        EditPhone        = string.Join(", ", value.AllPhones);
        EditWebsite      = value.Website;
        EditStreet       = value.Street;
        EditCity         = value.City;
        EditPostalCode   = value.PostalCode;
        EditCountry      = value.Country;
        EditNotes        = value.Notes;
    }

    [RelayCommand]
    private void NewContact()
    {
        IsNewContact = IsEditMode = HasSelection = true;
        SelectedContact  = null;
        EditDisplayName  = EditOrganization = EditDepartment = string.Empty;
        EditEmail        = EditPhone = EditWebsite = string.Empty;
        EditStreet       = EditCity = EditPostalCode = EditCountry = string.Empty;
        EditNotes        = StatusMessage = string.Empty;
    }

    [RelayCommand] private void Edit() => IsEditMode = true;

    [RelayCommand]
    private void CancelEdit()
    {
        if (IsNewContact) { IsNewContact = IsEditMode = HasSelection = false; return; }
        IsEditMode = false;
        if (SelectedContact is not null) OnSelectedContactChanged(SelectedContact);
    }

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

        var emails = SplitInput(EditEmail);
        var phones = SplitInput(EditPhone);

        if (IsNewContact)
        {
            var newContact = new Contact
            {
                DisplayName  = EditDisplayName,
                Organization = NullIfEmpty(EditOrganization),
                Department   = NullIfEmpty(EditDepartment),
                EmailsJson   = JsonSerializer.Serialize(emails),
                PhonesJson   = JsonSerializer.Serialize(phones),
                Website      = NullIfEmpty(EditWebsite),
                Street       = NullIfEmpty(EditStreet),
                City         = NullIfEmpty(EditCity),
                PostalCode   = NullIfEmpty(EditPostalCode),
                Country      = NullIfEmpty(EditCountry),
                Notes        = NullIfEmpty(EditNotes),
            };
            db.Add(newContact);
            await db.SaveChangesAsync();

            if (SelectedGroup is not null && SelectedGroup.Id != Guid.Empty)
            {
                db.Add(new ContactGroupMember
                {
                    GroupId   = SelectedGroup.Id,
                    ContactId = newContact.Id,
                });
                await db.SaveChangesAsync();
            }
        }
        else if (SelectedContact is not null)
        {
            var c = db.Contacts.FirstOrDefault(x => x.Id == SelectedContact.Id);
            if (c is not null)
            {
                c.DisplayName  = EditDisplayName;
                c.Organization = NullIfEmpty(EditOrganization);
                c.Department   = NullIfEmpty(EditDepartment);
                c.EmailsJson   = JsonSerializer.Serialize(emails);
                c.PhonesJson   = JsonSerializer.Serialize(phones);
                c.Website      = NullIfEmpty(EditWebsite);
                c.Street       = NullIfEmpty(EditStreet);
                c.City         = NullIfEmpty(EditCity);
                c.PostalCode   = NullIfEmpty(EditPostalCode);
                c.Country      = NullIfEmpty(EditCountry);
                c.Notes        = NullIfEmpty(EditNotes);
                c.UpdatedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
        }

        StatusMessage = "✓ Gespeichert";
        IsEditMode = IsNewContact = false;
        LoadContacts();
    }

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
        LoadContacts();
    }

    [RelayCommand]
    private void WriteMail()
    {
        if (SelectedContact is null || App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        var account = db.Accounts.FirstOrDefault();
        if (account is null) return;

        var vm  = new ComposeViewModel { AccountId = account.Id, To = SelectedContact.PrimaryEmail };
        var win = new ComposeWindow { DataContext = vm };
        vm.OnSent      += () => win.Close();
        vm.OnCancelled += () => win.Close();
        win.Show();
    }

    [RelayCommand]
    private void NewGroup()
    {
        IsGroupEditMode = IsNewGroup = true;
        EditGroupName   = string.Empty;
        EditGroupIcon   = "👥";
    }

    [RelayCommand]
    private void EditGroup()
    {
        if (SelectedGroup is null || SelectedGroup.Id == Guid.Empty) return;
        IsGroupEditMode = true;
        IsNewGroup      = false;
        EditGroupName   = SelectedGroup.Name;
        EditGroupIcon   = SelectedGroup.Icon;
    }

    [RelayCommand]
    private async Task SaveGroupAsync()
    {
        if (string.IsNullOrWhiteSpace(EditGroupName)) return;
        if (App.Services is null) return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        if (IsNewGroup)
        {
            db.Add(new ContactGroup { Name = EditGroupName, Icon = EditGroupIcon });
        }
        else if (SelectedGroup is not null && SelectedGroup.Id != Guid.Empty)
        {
            var g = db.ContactGroups.FirstOrDefault(x => x.Id == SelectedGroup.Id);
            if (g is not null) { g.Name = EditGroupName; g.Icon = EditGroupIcon; }
        }

        await db.SaveChangesAsync();
        IsGroupEditMode = IsNewGroup = false;
        LoadGroups();
    }

    [RelayCommand]
    private async Task DeleteGroupAsync()
    {
        if (SelectedGroup is null || SelectedGroup.Id == Guid.Empty || App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();
        var g = db.ContactGroups.FirstOrDefault(x => x.Id == SelectedGroup.Id);
        if (g is null) return;
        db.Remove(g);
        await db.SaveChangesAsync();
        LoadGroups();
    }

    [RelayCommand] private void CancelGroupEdit() => IsGroupEditMode = IsNewGroup = false;

    private static string[] SplitInput(string s) =>
        s.Split(',', ';').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();

    private static string? NullIfEmpty(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;

    private static string[] TryParseJson(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch { return []; }
    }
}