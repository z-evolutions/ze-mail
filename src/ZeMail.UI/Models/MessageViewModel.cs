using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ZeMail.UI.Models;

public partial class MessageViewModel : ObservableObject
{
    public Guid     Id             { get; init; }
    public string   Subject        { get; init; } = string.Empty;
    public string   FromName       { get; init; } = string.Empty;
    public string   FromAddress    { get; init; } = string.Empty;
    public DateTime ReceivedAtUtc  { get; init; }
    public bool     HasAttachments { get; init; }

    [ObservableProperty]
    private string? _bodyText;

    [ObservableProperty]
    private string? _bodyHtml;

    [ObservableProperty]
    private bool _isRead;

    [ObservableProperty]
    private bool _isStarred;

    public string DisplayDate => ReceivedAtUtc.ToLocalTime() switch
    {
        var d when d.Date == DateTime.Today             => d.ToString("HH:mm"),
        var d when d.Date == DateTime.Today.AddDays(-1) => "Gestern",
        var d                                           => d.ToString("dd.MM.yyyy")
    };

    public string SenderDisplay => string.IsNullOrEmpty(FromName) ? FromAddress : FromName;
}