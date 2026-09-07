using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KanTab.Models;

public class Board : INotifyPropertyChanged
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? ArchivedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public ObservableCollection<KanBanColumn> Columns { get; set; } = new();

    /// <summary>True when the board has been archived but not deleted.</summary>
    public bool IsArchived => ArchivedAt.HasValue && !DeletedAt.HasValue;

    /// <summary>True when the board has been deleted (soft delete).</summary>
    public bool IsDeleted => DeletedAt.HasValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void NotifyPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}