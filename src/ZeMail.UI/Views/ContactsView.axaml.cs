using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using ZeMail.Core.Entities;
using ZeMail.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace ZeMail.UI.Views;

public partial class ContactsView : UserControl
{
    public ContactsView()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Website klickbar
        var websiteBlock = this.FindControl<TextBlock>("WebsiteText");
        if (websiteBlock is not null)
        {
            websiteBlock.Cursor = new Cursor(StandardCursorType.Hand);
            websiteBlock.PointerPressed += OnWebsiteClicked;
        }

        // Drag-Source: Kontaktliste
        var contactList = this.FindControl<ListBox>("ContactListBox");
        if (contactList is not null)
        {
            contactList.AddHandler(PointerPressedEvent, OnContactPointerPressed,
                Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        // Drop-Target: Gruppenliste
        var groupList = this.FindControl<ListBox>("GroupListBox");
        if (groupList is not null)
        {
            DragDrop.SetAllowDrop(groupList, true);
            groupList.AddHandler(DragDrop.DropEvent,     OnGroupDrop);
            groupList.AddHandler(DragDrop.DragOverEvent, OnGroupDragOver);
        }
    }

    // ── Drag starten ─────────────────────────────────────────────────────────
    private async void OnContactPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ContactsViewModel vm) return;
        if (vm.SelectedContact is null) return;

        await System.Threading.Tasks.Task.Delay(150);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var item = new DataTransferItem();
        item.Set(DataFormat.Text, vm.SelectedContact.Id.ToString());

        var dragData = new DataTransfer();
        dragData.Add(item);

        await DragDrop.DoDragDropAsync(e, dragData, DragDropEffects.Move);
    }

    // ── Drag Over ────────────────────────────────────────────────────────────
    private void OnGroupDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = DragDropEffects.Move;
    }

    // ── Drop auf Gruppe ───────────────────────────────────────────────────────
    private async void OnGroupDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not ContactsViewModel vm) return;

        // ContactId aus Text lesen
        var contactIdStr = e.DataTransfer.TryGetText();
        if (!Guid.TryParse(contactIdStr, out var contactId)) return;

        // Ziel-Gruppe aus Drop-Position ermitteln
        var groupList = sender as ListBox;
        if (groupList is null) return;

        var pos   = e.GetPosition(groupList);
        var items = groupList.GetVisualDescendants().OfType<ListBoxItem>().ToList();

        ContactGroupViewModel? targetGroup = null;
        foreach (var item in items)
        {
            var pt = item.TranslatePoint(new Point(0, 0), groupList);
            if (pt is null) continue;
            if (pos.Y >= pt.Value.Y && pos.Y <= pt.Value.Y + item.Bounds.Height)
            {
                targetGroup = item.DataContext as ContactGroupViewModel;
                break;
            }
        }

        if (targetGroup is null) return;

        if (App.Services is null) return;
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider
                      .GetRequiredService<ZeMail.Core.Interfaces.IZeMailDbContext>();

        var existing = db.ContactGroupMembers
                         .Where(m => m.ContactId == contactId).ToList();
        foreach (var m in existing)
            db.Remove(m);

        if (targetGroup.Id != Guid.Empty)
        {
            db.Add(new ContactGroupMember
            {
                ContactId = contactId,
                GroupId   = targetGroup.Id,
            });
        }

        await db.SaveChangesAsync();
        vm.LoadContacts();
    }

    private void OnWebsiteClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not ContactsViewModel vm) return;
        var url = vm.SelectedContact?.Website;
        if (string.IsNullOrEmpty(url)) return;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        TopLevel.GetTopLevel(this)?.Launcher.LaunchUriAsync(new Uri(url));
    }
}