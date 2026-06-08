using System;
using System.Collections.ObjectModel;

namespace ZeMail.UI.Models;

public class FolderViewModel
{
    public Guid   Id            { get; init; }
    public string Name          { get; init; } = string.Empty;
    public string FullPath      { get; init; } = string.Empty;
    public string AccountName   { get; init; } = string.Empty;
    public bool   IsAccountHeader { get; init; } = false;

    public string Icon => Name.ToLower() switch
    {
        "inbox"   or "posteingang" => "📥",
        "sent"    or "gesendet"    => "📤",
        "drafts"  or "entwürfe"   => "📝",
        "trash"   or "papierkorb" => "🗑",
        "spam"    or "junk"       => "⚠",
        _                          => "📁"
    };

    public ObservableCollection<FolderViewModel> Children { get; } = [];
}