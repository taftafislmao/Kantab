using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace KanTab.Models;

public partial class ChecklistItem : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    partial void OnTextChanged(string value)
    {
        UpdatedAt = DateTime.Now;
    }

    partial void OnIsCompletedChanged(bool value)
    {
        UpdatedAt = DateTime.Now;
    }
}
