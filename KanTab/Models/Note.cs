using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KanTab.Models;

/// <summary>
/// A lightweight note persisted by the Notes workspace.
/// </summary>
public partial class Note : ObservableObject
{
    [ObservableProperty]
    private string _id = Guid.NewGuid().ToString();

    [ObservableProperty]
    private string _title = "Untitled note";

    [ObservableProperty]
    private string _content = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    public int Position { get; set; }

    [ObservableProperty]
    private DateTime _createdAt = DateTime.Now;

    [ObservableProperty]
    private DateTime _updatedAt = DateTime.Now;
}
